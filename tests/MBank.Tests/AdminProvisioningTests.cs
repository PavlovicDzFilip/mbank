using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MBank.Tests;

public sealed class AdminProvisioningTests(MBankWebApplicationFactory factory)
    : IClassFixture<MBankWebApplicationFactory>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Admin_endpoints_require_admin_bearer_token()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/admin/projects", new { name = "Unauthenticated" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Authorization: Bearer", body);
    }

    [Fact]
    public async Task Admin_can_create_user_project_and_project_token()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestDatabase.AdminToken);

        var userResponse = await client.PostAsJsonAsync("/admin/users", new
        {
            name = "Pavlo",
            email = "pavlo@example.test"
        });
        var user = await ReadObjectAsync(userResponse);
        Assert.Equal(HttpStatusCode.Created, userResponse.StatusCode);

        var projectResponse = await client.PostAsJsonAsync("/admin/projects", new
        {
            name = "MBank"
        });
        var project = await ReadObjectAsync(projectResponse);
        Assert.Equal(HttpStatusCode.Created, projectResponse.StatusCode);

        var tokenResponse = await client.PostAsJsonAsync("/admin/project-tokens", new
        {
            userId = user["id"]!.GetValue<Guid>(),
            projectId = project["id"]!.GetValue<Guid>(),
            name = "Codex repo token"
        });
        var token = await ReadObjectAsync(tokenResponse);

        Assert.Equal(HttpStatusCode.Created, tokenResponse.StatusCode);
        Assert.Equal("Crud", token["permissions"]!.GetValue<string>());
        Assert.StartsWith("mbp_", token["token"]!.GetValue<string>());
    }

    private static async Task<JsonObject> ReadObjectAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonNode.Parse(body)!.AsObject();
    }
}
