namespace CareOps.Domain.Credentialing;

public enum CredentialStatus
{
    Pending,
    Verified,
    Rejected,
    Expired
}

public enum VerificationResult
{
    Pending,
    Passed,
    Failed,
    NotApplicable
}
