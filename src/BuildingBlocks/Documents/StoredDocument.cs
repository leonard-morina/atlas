namespace Atlas.Documents;

/// <summary>Where a document was stored, and the SHA-256 of what was stored.</summary>
public sealed record StoredDocument(string BlobName, string Sha256, long SizeBytes);
