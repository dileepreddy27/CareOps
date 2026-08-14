using System.Text.RegularExpressions;
using CareOps.Domain.Common;

namespace CareOps.Domain.Credentialing;

public sealed partial class CredentialDocument : Entity
{
    private CredentialDocument() { }

    public CredentialDocument(
        Guid providerProfileId,
        string type,
        string originalFileName,
        string storageKey,
        string contentType,
        long sizeBytes,
        string sha256,
        DateOnly issuedOn,
        DateOnly expiresOn,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new DomainException("Credential type is required.");
        if (string.IsNullOrWhiteSpace(originalFileName)) throw new DomainException("A display file name is required.");
        if (string.IsNullOrWhiteSpace(storageKey)) throw new DomainException("A non-public storage key is required.");
        if (sizeBytes is <= 0 or > 25 * 1024 * 1024) throw new DomainException("Credential files must be between 1 byte and 25 MB.");
        if (!Sha256Pattern().IsMatch(sha256)) throw new DomainException("SHA-256 must be a 64-character hexadecimal digest.");
        if (expiresOn <= issuedOn) throw new DomainException("Credential expiration must be after its issue date.");

        ProviderProfileId = providerProfileId;
        Type = type.Trim();
        OriginalFileName = Path.GetFileName(originalFileName.Trim());
        StorageKey = storageKey.Trim();
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        Sha256 = sha256.ToLowerInvariant();
        IssuedOn = issuedOn;
        ExpiresOn = expiresOn;
        Status = CredentialStatus.Pending;
        CreatedAt = UpdatedAt = now;
    }

    public Guid ProviderProfileId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DateOnly IssuedOn { get; private set; }
    public DateOnly ExpiresOn { get; private set; }
    public CredentialStatus Status { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }

    public bool IsExpired(DateOnly today) => ExpiresOn < today;
    public bool ExpiresWithin(DateOnly today, int days) => ExpiresOn >= today && ExpiresOn <= today.AddDays(days);

    public void Verify(Guid reviewerId, DateTimeOffset now)
    {
        if (IsExpired(DateOnly.FromDateTime(now.UtcDateTime)))
            throw new DomainException("An expired credential cannot be verified.");

        Status = CredentialStatus.Verified;
        VerifiedByUserId = reviewerId;
        VerifiedAt = now;
        Touch(now);
    }

    public void Reject(Guid reviewerId, DateTimeOffset now)
    {
        Status = CredentialStatus.Rejected;
        VerifiedByUserId = reviewerId;
        VerifiedAt = now;
        Touch(now);
    }

    public void MarkExpired(DateTimeOffset now)
    {
        Status = CredentialStatus.Expired;
        Touch(now);
    }

    [GeneratedRegex("^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
