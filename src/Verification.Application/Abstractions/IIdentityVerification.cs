using Atlas.Verification.Application.VerifyApplication;
using Atlas.Verification.Domain.Checks;

namespace Atlas.Verification.Application.Abstractions;

/// <summary>Document authenticity and face match. IDNow in production; implemented by Infrastructure.</summary>
public interface IIdentityVerification
{
    Task<IdentityCheck> VerifyAsync(
        IdentityDocumentType documentType,
        byte[] document,
        byte[] selfie,
        CancellationToken cancellationToken);
}
