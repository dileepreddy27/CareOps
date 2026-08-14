using CareOps.Api.Endpoints;
using CareOps.Api.Middleware;
using CareOps.Application;
using CareOps.Application.Auth;
using CareOps.Infrastructure;
using CareOps.Infrastructure.Data;
using CareOps.Infrastructure.Realtime;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "careops-api")
    .WriteTo.Console());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keyPath)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath)).SetApplicationName("CareOps");
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddHealthChecks().AddDbContextCheck<CareOpsDbContext>("postgresql", tags: ["ready"]);
builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("CareOps.Api", serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString()))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint)) tracing.AddOtlpExporter(options => options.Endpoint = endpoint);
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation();
        if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint)) metrics.AddOtlpExporter(options => options.Endpoint = endpoint);
    });

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapOpenApi();
app.MapGet("/", () => Results.Redirect("/index.html")).ExcludeFromDescription();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapAuthEndpoints();
app.MapProviderEndpoints();
app.MapDashboardEndpoints();
app.MapSchedulingEndpoints();
app.MapNotificationEndpoints();
app.MapHub<WorkflowHub>("/hubs/workflow");
app.MapFallbackToFile("index.html").ExcludeFromDescription();

await app.Services.InitializeDatabaseAsync(app.Configuration, app.Environment);
await app.RunAsync();

public partial class Program;
