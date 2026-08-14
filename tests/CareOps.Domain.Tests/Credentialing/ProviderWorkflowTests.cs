using CareOps.Domain.Common;
using CareOps.Domain.Credentialing;
using FluentAssertions;

namespace CareOps.Domain.Tests.Credentialing;

public sealed class ProviderWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid ProviderUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ReviewerId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid ManagerId = Guid.Parse("30000000-0000-0000-0000-000000000003");

    [Fact]
    public void Submit_requires_at_least_one_credential()
    {
        var provider = CreateProvider();

        var act = () => provider.Submit(ProviderUserId, Now);

        act.Should().Throw<DomainException>().WithMessage("*one credential*");
        provider.Status.Should().Be(WorkflowStatus.Draft);
    }

    [Fact]
    public void Submit_sets_a_three_business_day_sla()
    {
        var friday = new DateTimeOffset(2026, 8, 14, 14, 0, 0, TimeSpan.Zero);
        var provider = CreateProvider(friday);
        AddCredential(provider, friday, expiresInDays: 90);

        provider.Submit(ProviderUserId, friday);

        provider.Status.Should().Be(WorkflowStatus.Submitted);
        provider.SlaDueAt.Should().Be(new DateTimeOffset(2026, 8, 19, 14, 0, 0, TimeSpan.Zero));
        provider.AuditEvents.Should().Contain(x => x.Action == "workflow.transitioned" && x.Details.Contains("Draft -> Submitted"));
    }

    [Fact]
    public void Workflow_does_not_allow_skipping_review()
    {
        var provider = CreateProvider();
        AddCredential(provider, Now, 90);
        provider.Submit(ProviderUserId, Now);

        var act = () => provider.TransitionTo(WorkflowStatus.Approved, ManagerId, null, Now.AddHours(1));

        act.Should().Throw<DomainException>().WithMessage("*Submitted*Approved*not allowed*");
    }

    [Fact]
    public void Approval_requires_verified_credentials_and_passed_required_checks()
    {
        var provider = CreateProvider();
        AddCredential(provider, Now, 90);
        provider.Submit(ProviderUserId, Now);
        provider.TransitionTo(WorkflowStatus.UnderReview, ReviewerId, null, Now.AddHours(1));

        var act = () => provider.TransitionTo(WorkflowStatus.Approved, ManagerId, null, Now.AddHours(2));

        act.Should().Throw<DomainException>().WithMessage("*credentials*verified*");
        provider.Status.Should().Be(WorkflowStatus.UnderReview);
    }

    [Fact]
    public void Complete_record_can_be_approved_and_is_audited()
    {
        var provider = CreateReadyForApproval();

        provider.TransitionTo(WorkflowStatus.Approved, ManagerId, "Final manager sign-off.", Now.AddHours(3));

        provider.Status.Should().Be(WorkflowStatus.Approved);
        provider.SlaDueAt.Should().BeNull();
        provider.AuditEvents.Last().Details.Should().Contain("UnderReview -> Approved");
    }

    [Fact]
    public void Approved_profile_expires_when_a_credential_elapses()
    {
        var provider = CreateReadyForApproval(expiresInDays: 1);
        provider.TransitionTo(WorkflowStatus.Approved, ManagerId, null, Now.AddHours(3));

        provider.ExpireIfRequired(DateOnly.FromDateTime(Now.AddDays(2).UtcDateTime), Now.AddDays(2));

        provider.Status.Should().Be(WorkflowStatus.Expired);
        provider.Credentials.Single().Status.Should().Be(CredentialStatus.Expired);
        provider.AuditEvents.Last().Details.Should().Contain("Automatically expired");
    }

    [Fact]
    public void Credential_metadata_strips_directory_segments_from_display_name()
    {
        var provider = CreateProvider();
        var credential = new CredentialDocument(provider.Id, "License", "../../private/license.pdf", "providers/key.pdf", "application/pdf", 100, new string('f', 64), new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1), Now);

        credential.OriginalFileName.Should().Be("license.pdf");
        credential.StorageKey.Should().NotContain("private");
    }

    private static ProviderProfile CreateReadyForApproval(int expiresInDays = 90)
    {
        var provider = CreateProvider();
        var credential = AddCredential(provider, Now, expiresInDays);
        provider.Submit(ProviderUserId, Now);
        provider.AssignReviewer(ReviewerId, ManagerId, Now.AddMinutes(30));
        provider.TransitionTo(WorkflowStatus.UnderReview, ReviewerId, null, Now.AddHours(1));
        credential.Verify(ReviewerId, Now.AddHours(2));
        foreach (var item in provider.ChecklistItems)
            item.Complete(VerificationResult.Passed, "Primary source verified.", ReviewerId, Now.AddHours(2));
        return provider;
    }

    private static ProviderProfile CreateProvider(DateTimeOffset? now = null) =>
        new(ProviderUserId, "1234567890", "Maya", "Chen", "Cardiology", "Northeast", now ?? Now);

    private static CredentialDocument AddCredential(ProviderProfile provider, DateTimeOffset now, int expiresInDays)
    {
        var document = new CredentialDocument(provider.Id, "State medical license", "license.pdf", $"providers/{provider.Id:N}/license.pdf", "application/pdf", 128_000, new string('a', 64), DateOnly.FromDateTime(now.AddYears(-1).UtcDateTime), DateOnly.FromDateTime(now.AddDays(expiresInDays).UtcDateTime), now);
        provider.AddCredential(document, ProviderUserId, now);
        return document;
    }
}
