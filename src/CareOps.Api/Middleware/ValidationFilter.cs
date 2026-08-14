using FluentValidation;

namespace CareOps.Api.Middleware;

public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null) return Results.BadRequest(new { error = $"Request body for {typeof(T).Name} is required." });
        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (result.IsValid) return await next(context);
        return Results.ValidationProblem(result.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(x => x.Key, x => x.Select(error => error.ErrorMessage).ToArray()));
    }
}
