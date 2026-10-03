using Atlas.Onboarding.Api.Features.Applications;
using Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

namespace Atlas.Onboarding.UnitTests.Applications;

[TestClass]
public sealed class SubmitApplicationRequestMappingTests
{
    // A value added to the contract but not to the mapping would otherwise only fail at runtime.
    [TestMethod]
    public void Every_contract_identifier_type_maps_to_the_domain()
    {
        foreach (var type in Enum.GetValues<IdentifierType>())
        {
            Assert.AreEqual(type.ToString(), type.ToDomain().ToString());
        }
    }

    [TestMethod]
    public void Every_contract_document_type_maps_to_the_domain()
    {
        foreach (var type in Enum.GetValues<DocumentType>())
        {
            Assert.AreEqual(type.ToString(), type.ToDomain().ToString());
        }
    }

    [TestMethod]
    public void ToCommand_decodes_images_and_uses_the_market_from_country()
    {
        var request = new SubmitApplicationRequest(
            "Ana", "Petrova", new DateOnly(1991, 3, 4), "MB", "MKD",
            new IdentifierRequest(IdentifierType.NationalId, "0403991450014", null),
            "ana@example.com", "+38970123456",
            [new DocumentRequest(DocumentType.Passport, "aGVsbG8="), new DocumentRequest(DocumentType.Selfie, "aGVsbG8=")],
            TermsAccepted: true);
        var key = Guid.NewGuid();

        var command = request.ToCommand(key);

        Assert.AreEqual(key, command.IdempotencyKey);
        Assert.AreEqual("MB", command.Market);
        Assert.AreEqual("hello", System.Text.Encoding.UTF8.GetString(command.Documents[0].Content));
    }
}
