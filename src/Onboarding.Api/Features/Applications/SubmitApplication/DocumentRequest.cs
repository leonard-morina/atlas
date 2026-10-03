namespace Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

/// <param name="Type"><c>PASSPORT</c>, <c>ID_CARD</c> or <c>SELFIE</c>.</param>
/// <param name="Image">Base64-encoded image.</param>
public sealed record DocumentRequest(DocumentType? Type, string? Image);
