using Atlas.Onboarding.Api.Features.Applications.SubmitApplication;
using Atlas.Onboarding.Api.Validation;

namespace Atlas.Onboarding.UnitTests.Applications;

[TestClass]
public sealed class UnreadableBodyErrorsTests
{
    [TestMethod]
    [DataRow("$.identifier.type", "Must be one of: NATIONAL_ID, PASSPORT.")]
    [DataRow("$.documents[1].type", "Must be one of: PASSPORT, ID_CARD, SELFIE.")]
    [DataRow("$.dateOfBirth", "Must be a date in the form YYYY-MM-DD, e.g. 1991-03-04.")]
    [DataRow("$.termsAccepted", "Must be true or false.")]
    [DataRow("$.firstName", "Not a valid value for this field.")]
    [DataRow("$.noSuchField", "Not a valid value for this field.")]
    public void Describes_what_the_field_accepts(string jsonPath, string expected) =>
        Assert.AreEqual(expected, UnreadableBodyErrors.Describe(typeof(SubmitApplicationRequest), jsonPath));
}
