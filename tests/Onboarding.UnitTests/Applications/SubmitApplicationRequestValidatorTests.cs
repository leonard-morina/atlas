using Atlas.Onboarding.Api.Features.Applications;
using Atlas.Onboarding.Api.Features.Applications.SubmitApplication;
using Atlas.Onboarding.Domain.Markets;
using Atlas.Onboarding.Domain.Markets.Identifiers;
using FluentValidation.TestHelper;

namespace Atlas.Onboarding.UnitTests.Applications;

[TestClass]
public sealed class SubmitApplicationRequestValidatorTests
{
    private const string Image = "aGVsbG8=";

    private static readonly SubmitApplicationRequestValidator Validator = new(
        new SupportedMarkets(
            new Dictionary<string, MarketRules>
            {
                ["MB"] = new() { IdentifierScheme = IdentifierScheme.PersonalNumber },
                ["MF"] = new() { IdentifierScheme = IdentifierScheme.PersonalNumber, AcceptsPassport = true },
            },
            [new PersonalNumberValidator()]),
        TimeProvider.System);

    private static readonly SubmitApplicationRequest Valid = new(
        FirstName: "Ana",
        LastName: "Petrova",
        DateOfBirth: new DateOnly(1991, 3, 4),
        Country: "MB",
        Nationality: "MKD",
        Identifier: new IdentifierRequest(IdentifierType.NationalId, "0403991450014", null),
        Email: "ana@example.com",
        Phone: "+38970123456",
        Documents: [new DocumentRequest(DocumentType.Passport, Image), new DocumentRequest(DocumentType.Selfie, Image)],
        TermsAccepted: true);

    [TestMethod]
    public void Accepts_a_valid_application() =>
        Validator.TestValidate(Valid).ShouldNotHaveAnyValidationErrors();

    // The published OpenAPI contract marks every record parameter without a default value as required. If the
    // validator accepted a missing value for one of them, the documented contract and the behaviour would differ.
    [TestMethod]
    public void Every_field_the_contract_marks_required_is_enforced()
    {
        foreach (var name in RequiredParameters<SubmitApplicationRequest>())
        {
            Validator.TestValidate(With(Valid, name, null)).ShouldHaveValidationErrorFor(name);
        }

        foreach (var name in RequiredParameters<IdentifierRequest>())
        {
            Validator.TestValidate(Valid with { Identifier = With(Valid.Identifier!, name, null) })
                .ShouldHaveValidationErrorFor($"Identifier.{name}");
        }

        foreach (var name in RequiredParameters<DocumentRequest>())
        {
            Validator.TestValidate(Valid with { Documents = [With(Valid.Documents![0], name, null), Valid.Documents[1]] })
                .ShouldHaveValidationErrorFor($"Documents[0].{name}");
        }
    }

    private static IEnumerable<string> RequiredParameters<T>() =>
        typeof(T).GetConstructors().Single().GetParameters()
            .Where(parameter => !parameter.HasDefaultValue)
            .Select(parameter => parameter.Name!);

    // A copy of a positional record with one constructor argument replaced.
    private static T With<T>(T instance, string name, object? value)
    {
        var constructor = typeof(T).GetConstructors().Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => parameter.Name == name ? value : typeof(T).GetProperty(parameter.Name!)!.GetValue(instance))
            .ToArray();

        return (T)constructor.Invoke(arguments);
    }

    [TestMethod]
    public void Accepts_a_passport_in_MF_for_residents_without_a_personal_number() =>
        Validator.TestValidate(Valid with
        {
            Country = "MF",
            Nationality = "SYR",
            Identifier = new IdentifierRequest(IdentifierType.Passport, "N1234567", "SYR"),
        }).ShouldNotHaveAnyValidationErrors();

    [TestMethod]
    public void Rejects_a_passport_in_a_market_that_does_not_accept_one() =>
        Validator.TestValidate(Valid with { Identifier = new IdentifierRequest(IdentifierType.Passport, "N1234567", "SYR") })
            .ShouldHaveValidationErrorFor("Identifier.Type");

    [TestMethod]
    public void Rejects_a_passport_without_its_issuing_state() =>
        Validator.TestValidate(Valid with
        {
            Country = "MF",
            Identifier = new IdentifierRequest(IdentifierType.Passport, "N1234567", null),
        }).ShouldHaveValidationErrorFor("Identifier.IssuingCountry");

    [TestMethod]
    public void Rejects_an_invalid_national_id_as_input_error() =>
        Validator.TestValidate(Valid with { Identifier = new IdentifierRequest(IdentifierType.NationalId, "0403991450016", null) })
            .ShouldHaveValidationErrorFor("Identifier.Value");

    [TestMethod]
    public void Rejects_an_unsupported_market() =>
        Validator.TestValidate(Valid with { Country = "MZ" }).ShouldHaveValidationErrorFor(r => r.Country);

    [TestMethod]
    public void Rejects_two_selfies() =>
        Validator.TestValidate(Valid with
        {
            Documents = [new DocumentRequest(DocumentType.Selfie, Image), new DocumentRequest(DocumentType.Selfie, Image)],
        }).ShouldHaveValidationErrorFor(r => r.Documents);

    [TestMethod]
    public void Rejects_an_image_that_is_not_base64() =>
        Validator.TestValidate(Valid with
        {
            Documents = [new DocumentRequest(DocumentType.Passport, "not base64!"), new DocumentRequest(DocumentType.Selfie, Image)],
        }).ShouldHaveValidationErrorFor("Documents[0].Image");

    [TestMethod]
    public void Rejects_terms_not_accepted() =>
        Validator.TestValidate(Valid with { TermsAccepted = false }).ShouldHaveValidationErrorFor(r => r.TermsAccepted);
}
