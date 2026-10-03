using Atlas.Onboarding.Domain.Markets.Identifiers;

namespace Atlas.Onboarding.UnitTests.Markets;

/// <summary>Test vectors from CDS-ID-04 §4, plus the failure cases each algorithm must catch.</summary>
[TestClass]
public sealed class NationalIdValidatorTests
{
    private static readonly DateOnly AnyDate = new(1990, 1, 1);

    [TestMethod]
    [DataRow("0403991450014", "1991-03-04", DisplayName = "MA/MB vector 1")]
    [DataRow("1512984702013", "1984-12-15", DisplayName = "MF citizen vector")]
    public void PersonalNumber_accepts_the_sheet_vectors(string value, string dateOfBirth) =>
        Assert.IsNull(new PersonalNumberValidator().Validate(value, DateOnly.Parse(dateOfBirth)));

    [TestMethod]
    public void PersonalNumber_sheet_vector_2_contradicts_its_own_date_of_birth()
    {
        // CDS-ID-04 §4 lists 2307980312076 with 1998-07-23, but DDMMYYY encodes 23-07-(1)980.
        // §1.4 requires the two to agree, so with the listed date it is rejected (open question).
        var validator = new PersonalNumberValidator();

        Assert.AreEqual("Does not match the date of birth.", validator.Validate("2307980312076", new DateOnly(1998, 7, 23)));
        Assert.IsNull(validator.Validate("2307980312076", new DateOnly(1980, 7, 23)));
    }

    [TestMethod]
    public void PersonalNumber_rejects_the_example_from_the_ticket()
    {
        // ATLAS-1's sample payload uses 0403991450016; §3.1 gives check digit 4 for these first twelve digits.
        var error = new PersonalNumberValidator().Validate("0403991450016", new DateOnly(1991, 3, 4));

        Assert.AreEqual("Check digit does not match.", error);
    }

    [TestMethod]
    public void PersonalNumber_rejects_m_equal_10_which_is_never_issued() =>
        Assert.AreEqual("Check digit does not match.", new PersonalNumberValidator().Validate("0403991450090", new DateOnly(1991, 3, 4)));

    [TestMethod]
    public void PersonalNumber_rejects_a_date_of_birth_that_differs_from_the_encoded_one() =>
        Assert.AreEqual("Does not match the date of birth.", new PersonalNumberValidator().Validate("0403991450014", new DateOnly(1991, 3, 5)));

    [TestMethod]
    [DataRow("040399145001", DisplayName = "12 digits")]
    [DataRow("04039914500140", DisplayName = "14 digits")]
    [DataRow("04039914500A4", DisplayName = "letter")]
    public void PersonalNumber_rejects_wrong_format(string value) =>
        Assert.AreEqual("Must be 13 digits.", new PersonalNumberValidator().Validate(value, AnyDate));

    [TestMethod]
    [DataRow("48215731A")]
    [DataRow("00321987X", DisplayName = "leading zeros are significant")]
    public void UnifiedCitizenId_accepts_the_sheet_vectors(string value) =>
        Assert.IsNull(new UnifiedCitizenIdValidator().Validate(value, AnyDate));

    [TestMethod]
    public void UnifiedCitizenId_rejects_a_wrong_check_letter() =>
        Assert.AreEqual("Check letter does not match.", new UnifiedCitizenIdValidator().Validate("48215731B", AnyDate));

    [TestMethod]
    [DataRow("48215731a", DisplayName = "lower-case letter")]
    [DataRow("4821573A1", DisplayName = "letter not in position 9")]
    [DataRow("482157311", DisplayName = "no letter")]
    public void UnifiedCitizenId_rejects_wrong_format(string value) =>
        Assert.AreEqual("Must be 8 digits followed by an upper-case letter.", new UnifiedCitizenIdValidator().Validate(value, AnyDate));

    [TestMethod]
    [DataRow("7321459080")]
    [DataRow("5849301275")]
    public void CitizenNumber_accepts_the_sheet_vectors(string value) =>
        Assert.IsNull(new CitizenNumberValidator().Validate(value, AnyDate));

    [TestMethod]
    public void CitizenNumber_rejects_a_wrong_check_digit() =>
        Assert.AreEqual("Check digit does not match.", new CitizenNumberValidator().Validate("7321459081", AnyDate));

    [TestMethod]
    public void CivilNumber_checks_format_only_because_the_algorithm_is_not_published()
    {
        Assert.IsNull(new CivilNumberValidator().Validate("1234567890", AnyDate));
        Assert.AreEqual("Must be 10 digits.", new CivilNumberValidator().Validate("123456789", AnyDate));
    }
}
