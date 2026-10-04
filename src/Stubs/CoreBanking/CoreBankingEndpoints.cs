using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Atlas.Stubs.Calls;
using Microsoft.Extensions.Options;

namespace Atlas.Stubs.CoreBanking;

/// <summary>
/// Stands in for the core banking ACCOUNTS service (CBS integration page v3.1): SOAP 1.1 over HTTP, one endpoint per
/// market, OpenAccount and FindCustomerAccounts, with the behaviour the page documents: no idempotency, a call ceiling
/// per market, an end-of-day window in market time, lookups by national ID only and served from a delayed replica.
/// The scenario is a marker word in the last name: Timeout (the account is opened, but only after the client gave
/// up), Busy (CONCURRENCY_LIMIT twice, then success), Invalid (VALIDATION) or Eod (EOD_IN_PROGRESS at any hour).
/// </summary>
public static partial class CoreBankingEndpoints
{
    public const string Provider = "corebanking";

    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Accounts = "urn:cbs:accounts:v3";

    public static IEndpointRouteBuilder MapCoreBanking(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/corebanking/{market}/accounts", HandleAsync);

        var state = endpoints.MapGroup("/_stub/corebanking");
        state.MapGet("", (CoreBankingLedger ledger) => ledger.Snapshot());
        state.MapDelete("", (CoreBankingLedger ledger) =>
        {
            ledger.Clear();
            return TypedResults.NoContent();
        });

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        string market,
        HttpRequest request,
        CoreBankingLedger ledger,
        CallLog calls,
        IOptions<CoreBankingOptions> options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        XElement? operation;
        try
        {
            var envelope = await XDocument.LoadAsync(request.Body, LoadOptions.None, cancellationToken);
            operation = envelope.Descendants(Soap + "Body").Elements().FirstOrDefault();
        }
        catch (XmlException)
        {
            return Fault("VALIDATION", "Malformed SOAP envelope.");
        }

        if (!options.Value.MarketTimeZones.ContainsKey(market))
        {
            return Fault("VALIDATION", $"Unknown market '{market}'.");
        }

        return operation?.Name.LocalName switch
        {
            "OpenAccount" => await OpenAccountAsync(market, operation, ledger, calls, options.Value, time),
            "FindCustomerAccounts" => FindCustomerAccounts(market, operation, ledger, calls),
            _ => Fault("VALIDATION", "Unknown operation."),
        };
    }

    private static async Task<IResult> OpenAccountAsync(
        string market,
        XElement operation,
        CoreBankingLedger ledger,
        CallLog calls,
        CoreBankingOptions options,
        TimeProvider time)
    {
        var firstName = Value(operation, "FirstName");
        var lastName = Value(operation, "LastName");
        var nationalId = Value(operation, "NationalId");
        var passportNumber = Value(operation, "PassportNumber");
        var channelReference = Value(operation, "ChannelReference");

        var scenario = Marker().Match(lastName ?? "") is { Success: true } match
            ? match.Groups["scenario"].Value.ToUpperInvariant()
            : "NORMAL";
        calls.Record(Provider, $"OpenAccount {scenario}");

        if (firstName is null || lastName is null || Value(operation, "DateOfBirth") is null
            || (nationalId is null && passportNumber is null) || channelReference is { Length: > 32 }
            || scenario == "INVALID")
        {
            return Fault("VALIDATION", "Request rejected before processing.");
        }

        if (scenario == "EOD" || (options.EnforceEndOfDay && IsEndOfDay(market, options, time)))
        {
            return Fault("EOD_IN_PROGRESS", "Market is in the end-of-day window.");
        }

        if (scenario == "BUSY" && ledger.CountAttempt(channelReference ?? lastName) <= 2)
        {
            return Fault("CONCURRENCY_LIMIT", "Concurrency ceiling reached.");
        }

        if (!ledger.TryEnter(market, options.CallCeilingPerMarket - options.BranchCallsInFlight))
        {
            return Fault("CONCURRENCY_LIMIT", "Concurrency ceiling reached.");
        }

        try
        {
            // The core keeps working when the caller stops waiting (§4): deliberately not the request's token.
            await Task.Delay(scenario == "TIMEOUT" ? options.TimeoutScenarioDelay : options.OpenAccountDelay, time, CancellationToken.None);

            var account = ledger.Open(market, nationalId, passportNumber, firstName, lastName, channelReference, options.ReplicaDelay);

            return Envelope(new XElement(Accounts + "OpenAccountResponse",
                new XElement(Accounts + "AccountNumber", account.AccountNumber)));
        }
        finally
        {
            ledger.Exit(market);
        }
    }

    private static IResult FindCustomerAccounts(string market, XElement operation, CoreBankingLedger ledger, CallLog calls)
    {
        calls.Record(Provider, "FindCustomerAccounts");

        // §2: lookups by passport number are on the roadmap (CBS-4471), not available.
        if (Value(operation, "NationalId") is not { } nationalId)
        {
            return Fault("VALIDATION", "Customers can only be looked up by national ID.");
        }

        return Envelope(new XElement(Accounts + "FindCustomerAccountsResponse",
            ledger.VisibleFor(market, nationalId).Select(account => new XElement(Accounts + "Account",
                new XElement(Accounts + "AccountNumber", account.AccountNumber),
                new XElement(Accounts + "ChannelReference", account.ChannelReference),
                new XElement(Accounts + "OpenedAt", account.OpenedAt.ToString("O"))))));
    }

    private static bool IsEndOfDay(string market, CoreBankingOptions options, TimeProvider time)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(options.MarketTimeZones[market]);
        var local = TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).TimeOfDay;
        return local >= TimeSpan.FromHours(22) || local < TimeSpan.FromHours(6);
    }

    private static string? Value(XElement operation, string name) =>
        operation.Element(Accounts + name)?.Value is { Length: > 0 } value ? value : null;

    private static IResult Envelope(XElement body, int statusCode = StatusCodes.Status200OK) =>
        Results.Content(
            new XDocument(new XElement(Soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soap", Soap),
                new XAttribute(XNamespace.Xmlns + "acc", Accounts),
                new XElement(Soap + "Body", body))).ToString(SaveOptions.DisableFormatting),
            "text/xml",
            Encoding.UTF8,
            statusCode);

    // SOAP 1.1 faults travel with HTTP 500; the documented fault code is in the detail.
    private static IResult Fault(string code, string message) =>
        Envelope(
            new XElement(Soap + "Fault",
                new XElement("faultcode", "soap:Server"),
                new XElement("faultstring", message),
                new XElement("detail", new XElement(Accounts + "Code", code))),
            StatusCodes.Status500InternalServerError);

    [GeneratedRegex(@"\b(?<scenario>Timeout|Busy|Invalid|Eod)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Marker();
}
