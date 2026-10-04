using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Atlas.Accounts.Application;
using Atlas.Accounts.Application.Abstractions;
using Atlas.Accounts.Domain.Openings;
using Microsoft.Extensions.Options;

namespace Atlas.Accounts.Infrastructure.CoreBanking;

/// <summary>
/// The ACCOUNTS service over SOAP 1.1 (CBS §1). Every failure is classified rather than thrown, because what may be
/// done next depends on whether the request could have reached core banking.
/// </summary>
internal sealed class CoreBankingClient(HttpClient http, IOptions<AccountOpeningOptions> options) : ICoreBanking
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Accounts = "urn:cbs:accounts:v3";

    public async Task<CoreBankingResult<string>> OpenAccountAsync(
        string market,
        Customer customer,
        string channelReference,
        CancellationToken cancellationToken)
    {
        var request = new XElement(Accounts + "OpenAccount",
            new XElement(Accounts + "FirstName", customer.FirstName),
            new XElement(Accounts + "LastName", customer.LastName),
            new XElement(Accounts + "DateOfBirth", customer.DateOfBirth.ToString("yyyy-MM-dd")),
            customer.NationalId is { } nationalId
                ? new XElement(Accounts + "NationalId", nationalId)
                : new XElement(Accounts + "PassportNumber", customer.PassportNumber),
            new XElement(Accounts + "ChannelReference", channelReference));

        return await CallAsync(market, request, options.Value.OpenAccountTimeout, cancellationToken) switch
        {
            CoreBankingResult<XElement>.Answered(var response)
                when response.Element(Accounts + "AccountNumber")?.Value is { Length: > 0 } accountNumber =>
                new CoreBankingResult<string>.Answered(accountNumber),
            CoreBankingResult<XElement>.Answered => Unreadable<string>(),
            var failure => Failure<string>(failure),
        };
    }

    public async Task<CoreBankingResult<IReadOnlyList<CustomerAccount>>> FindCustomerAccountsAsync(
        string market,
        string nationalId,
        CancellationToken cancellationToken)
    {
        var request = new XElement(Accounts + "FindCustomerAccounts", new XElement(Accounts + "NationalId", nationalId));

        return await CallAsync(market, request, options.Value.LookupTimeout, cancellationToken) switch
        {
            CoreBankingResult<XElement>.Answered(var response) =>
                new CoreBankingResult<IReadOnlyList<CustomerAccount>>.Answered(response.Elements(Accounts + "Account")
                    .Select(account => new CustomerAccount(
                        account.Element(Accounts + "AccountNumber")?.Value ?? "",
                        account.Element(Accounts + "ChannelReference")?.Value))
                    .ToList()),
            var failure => Failure<IReadOnlyList<CustomerAccount>>(failure),
        };
    }

    private async Task<CoreBankingResult<XElement>> CallAsync(
        string market,
        XElement operation,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        var envelope = new XDocument(new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", Soap),
            new XAttribute(XNamespace.Xmlns + "acc", Accounts),
            new XElement(Soap + "Body", operation)));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{market}/accounts");
        request.Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        request.Headers.Add("SOAPAction", $"\"{Accounts.NamespaceName}/{operation.Name.LocalName}\"");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));

        try
        {
            using var response = await http.SendAsync(request, deadline.Token);
            var reply = XDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
            var body = reply.Root?.Element(Soap + "Body")?.Elements().FirstOrDefault();

            if (body?.Name == Soap + "Fault")
            {
                return Fault(body);
            }

            return response.IsSuccessStatusCode && body is not null
                ? new CoreBankingResult<XElement>.Answered(body)
                : new CoreBankingResult<XElement>.Unanswered($"HTTP {(int)response.StatusCode} without a SOAP reply.");
        }
        catch (HttpRequestException exception) when (exception.HttpRequestError is
                                                         HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError)
        {
            // No connection, so no request: core banking did nothing.
            return new CoreBankingResult<XElement>.NotDelivered(exception.Message);
        }
        catch (HttpRequestException exception)
        {
            return new CoreBankingResult<XElement>.Unanswered(exception.Message);
        }
        catch (OperationCanceledException)
        {
            return new CoreBankingResult<XElement>.Unanswered(cancellationToken.IsCancellationRequested
                ? "The worker stopped while waiting for the answer."
                : $"No answer within {timeout}.");
        }
        catch (XmlException exception)
        {
            return new CoreBankingResult<XElement>.Unanswered($"The reply is not XML: {exception.Message}");
        }
    }

    // SOAP 1.1 faults arrive with HTTP 500; the documented code is in the detail (CBS §7).
    private static CoreBankingResult<XElement> Fault(XElement fault)
    {
        var message = fault.Element("faultstring")?.Value ?? "";

        return fault.Element("detail")?.Element(Accounts + "Code")?.Value switch
        {
            "EOD_IN_PROGRESS" => new CoreBankingResult<XElement>.Faulted(CoreBankingFault.EndOfDayInProgress, message),
            "CONCURRENCY_LIMIT" => new CoreBankingResult<XElement>.Faulted(CoreBankingFault.ConcurrencyLimit, message),
            "VALIDATION" => new CoreBankingResult<XElement>.Faulted(CoreBankingFault.Validation, message),
            // Behaviour the page does not document cannot be relied on (CBS footer): whether it did anything is unknown.
            var code => new CoreBankingResult<XElement>.Unanswered($"Undocumented fault {code ?? "(none)"}: {message}"),
        };
    }

    private static CoreBankingResult<T> Failure<T>(CoreBankingResult<XElement> failure) => failure switch
    {
        CoreBankingResult<XElement>.Faulted(var fault, var message) => new CoreBankingResult<T>.Faulted(fault, message),
        CoreBankingResult<XElement>.NotDelivered(var detail) => new CoreBankingResult<T>.NotDelivered(detail),
        CoreBankingResult<XElement>.Unanswered(var detail) => new CoreBankingResult<T>.Unanswered(detail),
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
    };

    private static CoreBankingResult<T> Unreadable<T>() =>
        new CoreBankingResult<T>.Unanswered("The reply has no account number.");
}
