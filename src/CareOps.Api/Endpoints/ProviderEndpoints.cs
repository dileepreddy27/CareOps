using CareOps.Api.Middleware;
using CareOps.Application.Credentialing;
using CareOps.Domain.Credentialing;
using CareOps.Infrastructure.Auth;

namespace CareOps.Api.Endpoints;

public static class ProviderEndpoints
{
    public static IEndpointRouteBuilder MapProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/providers").WithTags("Provider credentialing").RequireAuthorization();
        group.MapGet("/", GetQueueAsync).RequireAuthorization(policy => policy.RequireRole(AppRoles.CredentialingSpecialist, AppRoles.Manager, AppRoles.Administrator));
        group.MapGet("/me", GetMineAsync);
        group.MapGet("/{providerId:guid}", GetAsync);
        group.MapPut("/{providerId:guid}", UpdateAsync).AddEndpointFilter<ValidationFilter<UpdateProfileRequest>>();
        group.MapPost("/{providerId:guid}/credentials", AddCredentialAsync).AddEndpointFilter<ValidationFilter<AddCredentialRequest>>();
        group.MapPost("/{providerId:guid}/submit", SubmitAsync);
        group.MapPost("/{providerId:guid}/assign", AssignAsync).RequireAuthorization(policy => policy.RequireRole(AppRoles.CredentialingSpecialist, AppRoles.Manager, AppRoles.Administrator));
        group.MapPost("/{providerId:guid}/transition", TransitionAsync).RequireAuthorization(policy => policy.RequireRole(AppRoles.CredentialingSpecialist, AppRoles.Manager, AppRoles.Administrator));
        group.MapPost("/{providerId:guid}/credentials/{credentialId:guid}/review", ReviewCredentialAsync).RequireAuthorization(policy => policy.RequireRole(AppRoles.CredentialingSpecialist, AppRoles.Manager, AppRoles.Administrator));
        group.MapPut("/{providerId:guid}/checklist/{itemId:guid}", UpdateChecklistAsync).RequireAuthorization(policy => policy.RequireRole(AppRoles.CredentialingSpecialist, AppRoles.Manager, AppRoles.Administrator));
        group.MapPost("/{providerId:guid}/comments", AddCommentAsync).AddEndpointFilter<ValidationFilter<AddCommentRequest>>();
        return endpoints;
    }

    private static Task<PageResult<ProviderSummaryDto>> GetQueueAsync(string? search, WorkflowStatus? status, Guid? reviewerId, int page, int pageSize, ProviderWorkflowService service, CancellationToken ct) =>
        service.GetQueueAsync(new(search, status, reviewerId, page == 0 ? 1 : page, pageSize == 0 ? 25 : pageSize), ct);

    private static Task<ProviderDetailDto> GetMineAsync(HttpContext context, ProviderWorkflowService service, CancellationToken ct) =>
        service.GetMineAsync(context.User.UserId(), ct);

    private static Task<ProviderDetailDto> GetAsync(Guid providerId, HttpContext context, ProviderWorkflowService service, CancellationToken ct) =>
        service.GetAsync(providerId, context.User.UserId(), context.User.IsOperations(), ct);

    private static async Task<IResult> UpdateAsync(Guid providerId, UpdateProfileRequest request, HttpContext context, ProviderWorkflowService service, CancellationToken ct)
    {
        await service.UpdateProfileAsync(providerId, request, context.User.UserId(), context.User.IsOperations(), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AddCredentialAsync(Guid providerId, AddCredentialRequest request, HttpContext context, ProviderWorkflowService service, CancellationToken ct)
    {
        await service.AddCredentialAsync(providerId, request, context.User.UserId(), context.User.IsOperations(), ct);
        return Results.Accepted();
    }

    private static async Task<IResult> SubmitAsync(Guid providerId, HttpContext context, ProviderWorkflowService service, CancellationToken ct)
    {
        await service.SubmitAsync(providerId, context.User.UserId(), context.User.IsOperations(), ct);
        return Results.Accepted();
    }

    private static async Task<IResult> AssignAsync(Guid providerId, AssignReviewerRequest request, HttpContext context, ProviderWorkflowService service, CancellationToken ct)
    {
        await service.AssignAsync(providerId, request.ReviewerId, context.User.UserId(), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> TransitionAsync(Guid providerId, TransitionRequest request, HttpContext context, ProviderWorkflowService service, CancellationToken ct)
    {
        await service.TransitionAsync(providerId, request, context.User.UserId(), context.User.IsLeadership(), ct);
        return Results.Accepted();
    }

    private static async Task<IResult> ReviewCredentialAsync(Guid providerId, Guid credentialId, ReviewCredentialRequest request, HttpContext context, ProviderWorkflowService service, CancellationToken ct)
    {
        await service.ReviewCredentialAsync(providerId, credentialId, request, context.User.UserId(), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateChecklistAsync(Guid providerId, Guid itemId, UpdateChecklistRequest request, HttpContext context, ProviderWorkflowService service, CancellationToken ct)
    {
        await service.UpdateChecklistAsync(providerId, itemId, request, context.User.UserId(), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AddCommentAsync(Guid providerId, AddCommentRequest request, HttpContext context, ProviderWorkflowService service, CancellationToken ct)
    {
        await service.AddCommentAsync(providerId, request, context.User.UserId(), context.User.IsOperations(), ct);
        return Results.Accepted();
    }
}
