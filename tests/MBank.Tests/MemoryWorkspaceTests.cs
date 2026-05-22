using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MBank.Api.Domain;
using MBank.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MBank.Tests;

public sealed class MemoryWorkspaceTests(MBankWebApplicationFactory factory)
    : IClassFixture<MBankWebApplicationFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        factory.Services.GetRequiredService<FakeEmailSender>().Clear();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Memories_list_requires_sign_in()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync($"/projects/{Guid.NewGuid()}/memories");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_member_gets_not_found_for_list_and_detail()
    {
        var owner = await CreateSignedInClientAsync("owner.memories@example.test");
        var outsider = await CreateSignedInClientAsync("outsider.memories@example.test");

        var create = await PostAntiforgeryFormAsync(
            owner,
            "/projects",
            new Dictionary<string, string> { ["Name"] = "Memory Project" },
            "Create");
        var projectId = ExtractProjectId(create.Headers.Location!.OriginalString);

        var list = await outsider.GetAsync($"/projects/{projectId}/memories");
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);

        var detail = await outsider.GetAsync($"/projects/{projectId}/memories/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task Member_sees_starter_pack_in_list_for_new_project()
    {
        var client = await CreateSignedInClientAsync("starter.memories@example.test");
        var projectId = await CreateProjectAsync(client, "Starter Pack Memories");

        var response = await client.GetAsync($"/projects/{projectId}/memories");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("No memories in this project yet", body);
        Assert.Contains("MBank task startup", body);
        Assert.Contains("Sparring partner", body);
        Assert.Contains($"{ProjectStarterPack.Memories.Count} stored", body);
        Assert.Contains("View memories", await GetProjectDetailBodyAsync(client, projectId));
    }

    [Fact]
    public async Task Member_sees_memory_in_list_and_detail_after_mcp_remember()
    {
        var client = await CreateSignedInClientAsync("test@example.test");
        var projectId = TestDatabase.ProjectId.Value;

        var summary = "Operators can inspect stored memories in Razor Pages without MCP.";
        var memoryId = await RememberViaMcpAsync(summary);

        var list = await client.GetAsync($"/projects/{projectId}/memories");
        var listBody = await list.Content.ReadAsStringAsync();
        Assert.Contains(summary[..Math.Min(summary.Length, 160)], listBody);
        Assert.Contains("technical-decision", listBody);

        var detail = await client.GetAsync($"/projects/{projectId}/memories/{memoryId}");
        var detailBody = await detail.Content.ReadAsStringAsync();
        Assert.Contains(summary, detailBody);
        Assert.Contains("Source evidence", detailBody);
        Assert.Contains("https://example.test/mbank/operator-ui", detailBody);
    }

    [Fact]
    public async Task Detail_shows_conflict_link_after_mcp_report_conflict()
    {
        var client = await CreateSignedInClientAsync("test@example.test");
        var projectId = TestDatabase.ProjectId.Value;

        var firstId = await RememberViaMcpAsync(
            "Use Razor Pages for operator memory visibility.",
            type: "product-decision");
        var secondId = await RememberViaMcpAsync(
            "Use a SPA dashboard for operator memory visibility.",
            type: "product-decision");

        await ReportConflictViaMcpAsync(firstId, secondId, "Competing UI approaches recorded.");

        var detail = await client.GetAsync($"/projects/{projectId}/memories/{firstId}");
        var body = await detail.Content.ReadAsStringAsync();

        Assert.Contains("Conflict cluster", body);
        Assert.Contains("conflicts", body);
        Assert.Contains(secondId.ToString(), body);
    }

    [Fact]
    public async Task Superseded_memory_hidden_from_list_unless_toggle_enabled()
    {
        var client = await CreateSignedInClientAsync("test@example.test");
        var projectId = TestDatabase.ProjectId.Value;

        var keptId = await RememberViaMcpAsync("Keep this operator memory guidance.", type: "technical-rule");
        var supersededId = await RememberViaMcpAsync("Obsolete operator memory guidance.", type: "technical-rule");
        await ResolveConflictViaMcpAsync(keptId, supersededId, "The kept memory is authoritative.");

        var defaultList = await client.GetAsync($"/projects/{projectId}/memories");
        var defaultBody = await defaultList.Content.ReadAsStringAsync();
        Assert.DoesNotContain(supersededId.ToString(), defaultBody);

        var withSuperseded = await client.GetAsync($"/projects/{projectId}/memories?includeSuperseded=true");
        var supersededBody = await withSuperseded.Content.ReadAsStringAsync();
        Assert.Contains(supersededId.ToString(), supersededBody);
        Assert.Contains("superseded", supersededBody, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpClient> CreateSignedInClientAsync(string email)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var emailSender = factory.Services.GetRequiredService<FakeEmailSender>();
        emailSender.Clear();
        await RequestMagicLinkAsync(client, email);
        var link = ExtractMagicLink(emailSender.Messages.Single());
        var callback = await client.GetAsync(link.PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        return client;
    }

    private static async Task<HttpResponseMessage> RequestMagicLinkAsync(HttpClient client, string email)
    {
        var login = await client.GetAsync("/login");
        var html = await login.Content.ReadAsStringAsync();
        var token = ExtractAntiforgeryToken(html);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["__RequestVerificationToken"] = token
        });
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        return await client.PostAsync("/login", content);
    }

    private static async Task<HttpResponseMessage> PostAntiforgeryFormAsync(
        HttpClient client,
        string path,
        Dictionary<string, string> fields,
        string handler)
    {
        var page = await client.GetAsync(path);
        var html = await page.Content.ReadAsStringAsync();
        var token = ExtractAntiforgeryToken(html);
        fields["__RequestVerificationToken"] = token;

        var content = new FormUrlEncodedContent(fields);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        return await client.PostAsync($"{path}?handler={handler}", content);
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client, string name)
    {
        var create = await PostAntiforgeryFormAsync(
            client,
            "/projects",
            new Dictionary<string, string> { ["Name"] = name },
            "Create");
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        return ExtractProjectId(create.Headers.Location!.OriginalString);
    }

    private static async Task<string> GetProjectDetailBodyAsync(HttpClient client, Guid projectId)
    {
        var response = await client.GetAsync($"/projects/{projectId}");
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<Guid> RememberViaMcpAsync(
        string summary,
        string type = "technical-decision")
    {
        var client = factory.CreateClient();
        var remember = await CallToolAsync(client, "remember", new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["memories"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = type,
                        ["summary"] = summary,
                        ["subject"] = "operator UI",
                        ["tags"] = new JsonArray("operator-ui", "razor-pages"),
                        ["confidence"] = 0.95
                    }
                },
                ["source"] = new JsonObject
                {
                    ["sourceKind"] = "test",
                    ["sourceText"] = "Operator UI memory visibility test seed.",
                    ["sourceUrl"] = "https://example.test/mbank/operator-ui"
                }
            }
        });

        return remember["storedMemories"]!.AsArray().Single()!["id"]!.GetValue<Guid>();
    }

    private async Task ReportConflictViaMcpAsync(Guid firstId, Guid secondId, string description)
    {
        var client = factory.CreateClient();
        await CallToolAsync(client, "report_conflict", new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["memoryIds"] = new JsonArray(firstId, secondId),
                ["description"] = description
            }
        });
    }

    private async Task ResolveConflictViaMcpAsync(
        Guid keptId,
        Guid supersededId,
        string clarification)
    {
        var client = factory.CreateClient();
        await CallToolAsync(client, "resolve_conflict", new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["keptMemoryId"] = keptId,
                ["supersededMemoryIds"] = new JsonArray(supersededId),
                ["clarification"] = clarification
            }
        });
    }

    private static async Task<JsonObject> CallToolAsync(HttpClient client, string toolName, JsonObject arguments)
    {
        var result = await SendMcpRequestAsync(client, "tools/call", new JsonObject
        {
            ["name"] = toolName,
            ["arguments"] = arguments
        });

        var callResult = result["result"]!.AsObject();
        Assert.False(callResult["isError"]?.GetValue<bool>() ?? false);
        return GetStructuredContent(callResult);
    }

    private static JsonObject GetStructuredContent(JsonObject callResult)
    {
        if (callResult["structuredContent"] is JsonObject structuredContent)
        {
            return structuredContent;
        }

        var text = callResult["content"]!.AsArray()[0]!["text"]!.GetValue<string>();
        return JsonNode.Parse(text)!.AsObject();
    }

    private static async Task<JsonObject> SendMcpRequestAsync(HttpClient client, string method, JsonObject parameters)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Guid.NewGuid().ToString("N"),
            ["method"] = method,
            ["params"] = parameters
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestDatabase.ProjectToken);

        using var response = await client.SendAsync(message);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);

        return ParseMcpResponse(body);
    }

    private static JsonObject ParseMcpResponse(string body)
    {
        var json = body.TrimStart().StartsWith('{')
            ? body
            : ExtractSseData(body);

        var root = JsonNode.Parse(json)!.AsObject();
        if (root["error"] is not null)
        {
            throw new InvalidOperationException(root["error"]!.ToJsonString());
        }

        return root;
    }

    private static string ExtractSseData(string body)
    {
        using var reader = new StringReader(body);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                return line["data:".Length..].TrimStart();
            }
        }

        throw new InvalidOperationException("Could not parse MCP SSE response.");
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, "Could not find antiforgery token.");
        return System.Net.WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    private static Uri ExtractMagicLink(EmailMessage message)
    {
        var match = Regex.Match(message.TextBody, @"https?://\S+", RegexOptions.CultureInvariant);
        Assert.True(match.Success, "Could not find magic link in email body.");
        return new Uri(match.Value);
    }

    private static Guid ExtractProjectId(string location)
    {
        var match = Regex.Match(location, @"/projects/(?<id>[0-9a-f-]{36})", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Could not parse project id from '{location}'.");
        return Guid.Parse(match.Groups["id"].Value);
    }
}
