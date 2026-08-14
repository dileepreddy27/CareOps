using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareOps.Api.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CareOps.Api.IntegrationTests;

[Collection(CareOpsApiCollection.Name)]
public sealed class AuthenticationAndWorkflowTests(CareOpsApiFactory factory)
{
    [Fact]
    public async Task Readiness_probe_reaches_the_testcontainer_database()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Operations_dashboard_rejects_anonymous_requests()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Provider_can_register_and_read_only_their_own_profile()
    {
        var client = factory.CreateClient();
        var email = $"provider-{Guid.NewGuid():N}@example.test";
        var registration = await client.PostAsJsonAsync("/api/auth/register/provider", new
        {
            email,
            password = "Valid-Demo-Password1!",
            npi = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            firstName = "Taylor",
            lastName = "Morgan",
            specialty = "Internal Medicine",
            region = "Northeast"
        });
        registration.EnsureSuccessStatusCode();
        var payload = await registration.Content.ReadFromJsonAsync<JsonElement>();
        var token = payload.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var profile = await client.GetAsync("/api/providers/me");
        var dashboard = await client.GetAsync("/api/dashboard");

        profile.StatusCode.Should().Be(HttpStatusCode.OK);
        var profilePayload = await profile.Content.ReadFromJsonAsync<JsonElement>();
        profilePayload.GetProperty("displayName").GetString().Should().Be("Taylor Morgan");
        profilePayload.GetProperty("status").GetString().Should().Be("Draft", "enum values are a documented string contract");
        dashboard.StatusCode.Should().Be(HttpStatusCode.Forbidden, "provider role cannot access operational KPIs");
    }
}
