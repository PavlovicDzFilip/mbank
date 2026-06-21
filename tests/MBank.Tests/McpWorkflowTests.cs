using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MBank.Api.Services;

namespace MBank.Tests;

public sealed class McpWorkflowTests(MBankWebApplicationFactory factory)
    : IClassFixture<MBankWebApplicationFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Mcp_endpoint_rejects_missing_project_token()
    {
        var client = factory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = Guid.NewGuid().ToString("N"),
                ["method"] = "tools/list",
                ["params"] = new JsonObject()
            }, options: JsonOptions)
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await client.SendAsync(message);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("project access token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mcp_endpoint_returns_parse_error_for_malformed_json()
    {
        var client = factory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                "{jsonrpc:\"2.0\",id:\"malformed\",method:\"tools/list\",params:{}}",
                Encoding.UTF8,
                "application/json")
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestDatabase.ProjectToken);

        using var response = await client.SendAsync(message);
        var body = await response.Content.ReadAsStringAsync();
        var error = JsonNode.Parse(body)!.AsObject();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("2.0", error["jsonrpc"]!.GetValue<string>());
        Assert.Null(error["id"]);
        Assert.Equal(-32700, error["error"]!["code"]!.GetValue<int>());
        Assert.Equal("Parse error", error["error"]!["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task Mcp_endpoint_proves_agent_memory_workflow_end_to_end()
    {
        var client = factory.CreateClient();

        var tools = await SendMcpRequestAsync(client, "tools/list", new JsonObject());
        var startTaskTool = ResolveToolName(tools, "StartTask");
        var describeMemoryTypesTool = ResolveToolName(tools, "DescribeMemoryTypes");
        var rememberTool = ResolveToolName(tools, "Remember");
        var getTaskContextTool = ResolveToolName(tools, "GetTaskContext");
        var getTool = ResolveToolName(tools, "Get");
        var reportConflictTool = ResolveToolName(tools, "ReportConflict");
        var resolveConflictTool = ResolveToolName(tools, "ResolveConflict");
        var promoteMemoryTool = ResolveToolName(tools, "PromoteMemory");
        var checkOnboardingTool = ResolveToolName(tools, "CheckOnboarding");

        var rememberToolDefinition = ResolveTool(tools, rememberTool);
        Assert.Contains(
            "Roslyn analyzer",
            rememberToolDefinition["description"]!.GetValue<string>(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "why one approach was chosen instead of another",
            rememberToolDefinition["description"]!.GetValue<string>(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "experimental",
            rememberToolDefinition["description"]!.GetValue<string>(),
            StringComparison.OrdinalIgnoreCase);
        var rememberInputSchema = rememberToolDefinition["inputSchema"]!.AsObject();
        var memoryTypeSchema = rememberInputSchema["properties"]!["request"]!["properties"]!["memories"]!["items"]!["properties"]!["type"]!.AsObject();
        Assert.Equal("string", memoryTypeSchema["type"]!.GetValue<string>());
        Assert.Contains(memoryTypeSchema["enum"]!.AsArray(), x =>
            x!.GetValue<string>() == "product-decision");
        Assert.Contains(memoryTypeSchema["enum"]!.AsArray(), x =>
            x!.GetValue<string>() == "technical-rule");
        Assert.Contains(memoryTypeSchema["enum"]!.AsArray(), x =>
            x!.GetValue<string>() == "agent-instruction");
        var lifecycleSchema = rememberInputSchema["properties"]!["request"]!["properties"]!["memories"]!["items"]!["properties"]!["lifecycle"]!.AsObject();
        Assert.Contains(lifecycleSchema["enum"]!.AsArray(), x =>
            x!.GetValue<string>() == "accepted");
        Assert.Contains(lifecycleSchema["enum"]!.AsArray(), x =>
            x!.GetValue<string>() == "experimental");

        var promoteMemoryToolDefinition = ResolveTool(tools, promoteMemoryTool);
        Assert.Contains(
            "experimental memory may supersede accepted memories",
            promoteMemoryToolDefinition["description"]!.GetValue<string>(),
            StringComparison.OrdinalIgnoreCase);

        var startTask = await CallToolAsync(client, startTaskTool, new JsonObject());
        var startTaskContent = GetStructuredContent(startTask);
        Assert.Contains(startTaskContent["rules"]!.AsArray(), x =>
            x!["source"]!.GetValue<string>() == "canonical_instruction"
            && x["text"]!.GetValue<string>().Contains("get_task_context", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(startTaskContent["rules"]!.AsArray(), x =>
            x!["source"]!.GetValue<string>() == "canonical_instruction"
            && x["text"]!.GetValue<string>().Contains("uncertain how to proceed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(startTaskContent["rules"]!.AsArray(), x =>
            x!["source"]!.GetValue<string>() == "canonical_instruction"
            && x["text"]!.GetValue<string>().Contains("why one approach was chosen instead of another", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("this task", startTaskContent["summary"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AgentInstructionService.LatestInstructionVersion, startTaskContent["instructionStatus"]!["latestVersion"]!.GetValue<string>());
        Assert.Equal("unknown", startTaskContent["instructionStatus"]!["status"]!.GetValue<string>());
        Assert.Null(startTaskContent["instructionStatus"]!["files"]);

        var startTaskWithReportedVersion = await CallToolAsync(client, startTaskTool, new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["agentInstructionVersion"] = "0.9.0"
            }
        });
        var reportedVersionContent = GetStructuredContent(startTaskWithReportedVersion);
        Assert.Equal("outdated", reportedVersionContent["instructionStatus"]!["status"]!.GetValue<string>());
        Assert.Equal("0.9.0", reportedVersionContent["instructionStatus"]!["reportedVersion"]!.GetValue<string>());
        Assert.Null(reportedVersionContent["instructionStatus"]!["files"]);
        Assert.Contains("0.9.0", reportedVersionContent["instructionStatus"]!["summary"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("bootstrap", reportedVersionContent["instructionStatus"]!["recommendedAction"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var suppressedStartTask = await CallToolAsync(client, startTaskTool, new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["agentInstructionVersion"] = "0.9.0"
            }
        });
        var suppressedContent = GetStructuredContent(suppressedStartTask);
        Assert.Equal("current", suppressedContent["instructionStatus"]!["status"]!.GetValue<string>());
        Assert.Empty(suppressedContent["instructionStatus"]!["warnings"]!.AsArray());

        var memoryTypes = await CallToolAsync(client, describeMemoryTypesTool, new JsonObject());
        var memoryTypesContent = GetStructuredContent(memoryTypes);
        Assert.Equal(1, memoryTypesContent["modelVersion"]!.GetValue<int>());
        Assert.Contains(memoryTypesContent["types"]!.AsArray(), x =>
            x!["value"]!.GetValue<string>() == "product-decision"
            && x["description"]!.GetValue<string>().Contains("product choice", StringComparison.OrdinalIgnoreCase)
            && !x["isWarning"]!.GetValue<bool>()
            && !x["isStartupGuidance"]!.GetValue<bool>());
        Assert.Contains(memoryTypesContent["types"]!.AsArray(), x =>
            x!["value"]!.GetValue<string>() == "product-risk"
            && x["isWarning"]!.GetValue<bool>());
        Assert.Contains(memoryTypesContent["types"]!.AsArray(), x =>
            x!["value"]!.GetValue<string>() == "agent-instruction"
            && x["isStartupGuidance"]!.GetValue<bool>());
        Assert.Contains(memoryTypesContent["types"]!.AsArray(), x =>
            x!["value"]!.GetValue<string>() == "technical-rule"
            && !string.IsNullOrWhiteSpace(x["captureHint"]!.GetValue<string>()));
        Assert.Contains(memoryTypesContent["experimentalMemoryGuidance"]!["startSignals"]!.AsArray(), x =>
            x!.GetValue<string>().Contains("experiment", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(memoryTypesContent["experimentalMemoryGuidance"]!["startSignals"]!.AsArray(), x =>
            x!.GetValue<string>().Contains("thinking out loud", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(memoryTypesContent["experimentalMemoryGuidance"]!["endSignals"]!.AsArray(), x =>
            x!.GetValue<string>().Contains("promote", StringComparison.OrdinalIgnoreCase));

        var remember = await CallToolAsync(client, rememberTool, new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["memories"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "product-decision",
                        ["summary"] = "The MVP must prove the MCP memory loop before dashboard, UI, auth, billing, permissions, generic search, or enterprise work.",
                        ["subject"] = "MVP scope",
                        ["tags"] = new JsonArray("mvp", "mcp", "dashboard", "scope"),
                        ["confidence"] = 0.98
                    },
                    new JsonObject
                    {
                        ["type"] = "product-risk",
                        ["summary"] = "Do not start dashboard or UI work until the /mcp agent memory workflow is proven end to end.",
                        ["subject"] = "MVP scope",
                        ["tags"] = new JsonArray("ui", "dashboard", "mcp", "warning"),
                        ["confidence"] = 0.95
                    }
                },
                ["source"] = new JsonObject
                {
                    ["sourceKind"] = "test_conversation",
                    ["sourceText"] = "The user explicitly scoped the proof to black-box MCP workflow around /mcp and excluded UI, auth, generic search, and continuity files.",
                    ["sourceUrl"] = "https://example.test/mbank/mcp-proof"
                }
            }
        });

        var rememberContent = GetStructuredContent(remember);
        var storedMemories = rememberContent["storedMemories"]!.AsArray();
        Assert.Equal(2, storedMemories.Count);
        var decisionId = storedMemories
            .Single(x => x!["type"]!.GetValue<string>() == "product-decision")!["id"]!
            .GetValue<Guid>();
        var warningId = storedMemories
            .Single(x => x!["type"]!.GetValue<string>() == "product-risk")!["id"]!
            .GetValue<Guid>();

        var context = await CallToolAsync(client, getTaskContextTool, new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["task"] = new JsonObject
                {
                    ["title"] = "Decide whether to add a dashboard",
                    ["description"] = "A later agent session is considering dashboard, UI, auth, generic search, and enterprise features. Should it proceed before the MCP loop is proven?",
                    ["kind"] = "product-planning"
                },
                ["workArea"] = new JsonObject
                {
                    ["subject"] = "MVP scope",
                    ["modules"] = new JsonArray("MBank.Api", "MCP")
                },
                ["limit"] = 4
            }
        });

        var contextContent = GetStructuredContent(context);
        Assert.Equal("context_found", contextContent["status"]!.GetValue<string>());

        var strongMatches = contextContent["strongMatches"]!.AsArray();
        Assert.Contains(strongMatches, x => x!["id"]!.GetValue<Guid>() == decisionId);
        Assert.Contains(strongMatches, x => x!["id"]!.GetValue<Guid>() == warningId);
        Assert.Contains(strongMatches, x =>
            x!["id"]!.GetValue<Guid>() == decisionId
            && x["memoryConfidence"]!.GetValue<double>() > 0.9
            && x["matchStrength"]!.GetValue<string>() == "strong"
            && x["matchScore"]!.GetValue<double>() >= 0.75);
        Assert.All(strongMatches, x =>
        {
            Assert.False(x!.AsObject().ContainsKey("matchReason"));
            Assert.False(x.AsObject().ContainsKey("whyRelevant"));
        });
        Assert.NotNull(contextContent["possibleMatches"]);
        Assert.Null(contextContent["onboarding"]);
        Assert.Null(contextContent["instructionStatus"]);
        Assert.Null(contextContent["relevantMemories"]);
        Assert.Null(contextContent["warnings"]);
        Assert.Null(contextContent["conflicts"]);
        Assert.Null(contextContent["clarifyingQuestions"]);

        var onboarding = await CallToolAsync(client, checkOnboardingTool, new JsonObject());
        var onboardingContent = GetStructuredContent(onboarding);
        Assert.Equal(AgentInstructionService.LatestInstructionVersion, onboardingContent["latestVersion"]!.GetValue<string>());
        Assert.True(onboardingContent["requiresExplicitApproval"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(onboardingContent["recommendedAction"]!.GetValue<string>()));

        Assert.False(contextContent["canProceed"]!.GetValue<bool>());
        var attentionItems = contextContent["attentionItems"]!.AsArray();
        Assert.Contains(attentionItems, x =>
            x!["kind"]!.GetValue<string>() == "warning"
            && x["action"]!.GetValue<string>() == "notice"
            && x["summary"]!.GetValue<string>().Contains("/mcp agent memory workflow", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attentionItems, x =>
            x!["kind"]!.GetValue<string>() == "conflict"
            && x["action"]!.GetValue<string>() == "ask_user"
            && x["question"]!.GetValue<string>().Contains("Which one should guide this task?", StringComparison.OrdinalIgnoreCase)
            && x["memoryIds"]!.AsArray().Any(id => id!.GetValue<Guid>() == decisionId)
            && x["memoryIds"]!.AsArray().Any(id => id!.GetValue<Guid>() == warningId));
        Assert.Contains(attentionItems, x =>
            x!["kind"]!.GetValue<string>() == "capture_offer"
            && x["action"]!.GetValue<string>() == "notice");

        var detail = await CallToolAsync(client, getTool, new JsonObject
        {
            ["id"] = decisionId
        });

        var detailContent = GetStructuredContent(detail);
        Assert.Equal(decisionId, detailContent["id"]!.GetValue<Guid>());
        Assert.Equal("test_conversation", detailContent["source"]!["sourceKind"]!.GetValue<string>());
        Assert.Contains("black-box MCP workflow", detailContent["source"]!["sourceText"]!.GetValue<string>());

        var conflict = await CallToolAsync(client, reportConflictTool, new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["memoryIds"] = new JsonArray(decisionId, warningId),
                ["description"] = "The warning reinforces the decision, but agents should surface this pair before dashboard implementation."
            }
        });

        var conflictContent = GetStructuredContent(conflict);
        Assert.True(conflictContent["recorded"]!.GetValue<bool>());

        var detailAfterConflict = await CallToolAsync(client, getTool, new JsonObject
        {
            ["id"] = decisionId
        });

        var detailAfterConflictContent = GetStructuredContent(detailAfterConflict);
        Assert.Contains(detailAfterConflictContent["linkedMemoryIds"]!.AsArray(), x =>
            x!.GetValue<Guid>() == warningId);
        Assert.Contains(detailAfterConflictContent["links"]!.AsArray(), x =>
            x!["memoryId"]!.GetValue<Guid>() == warningId
            && x["relation"]!.GetValue<string>() == "conflicts"
            && x["description"]!.GetValue<string>().Contains("surface this pair", StringComparison.OrdinalIgnoreCase));

        var resolution = await CallToolAsync(client, resolveConflictTool, new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["keptMemoryId"] = decisionId,
                ["supersededMemoryIds"] = new JsonArray(warningId),
                ["clarification"] = "The user clarified that the decision is authoritative; the warning is obsolete."
            }
        });
        var resolutionContent = GetStructuredContent(resolution);
        Assert.True(resolutionContent["resolved"]!.GetValue<bool>());

        var detailAfterResolution = await CallToolAsync(client, getTool, new JsonObject
        {
            ["id"] = decisionId
        });
        var detailAfterResolutionContent = GetStructuredContent(detailAfterResolution);
        Assert.Contains(detailAfterResolutionContent["links"]!.AsArray(), x =>
            x!["memoryId"]!.GetValue<Guid>() == warningId
            && x["relation"]!.GetValue<string>() == "supersedes"
            && x["description"]!.GetValue<string>().Contains("authoritative", StringComparison.OrdinalIgnoreCase));

        var supersededDetail = await CallToolAsync(client, getTool, new JsonObject
        {
            ["id"] = warningId
        });
        Assert.Equal("superseded", GetStructuredContent(supersededDetail)["status"]!.GetValue<string>());

        var contextAfterResolution = await CallToolAsync(client, getTaskContextTool, new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["task"] = new JsonObject
                {
                    ["title"] = "Decide whether to add a dashboard after clarification",
                    ["description"] = "A later agent session is considering dashboard, UI, auth, generic search, and enterprise features. Should it proceed before the MCP loop is proven?",
                    ["kind"] = "product-planning"
                },
                ["workArea"] = new JsonObject
                {
                    ["subject"] = "MVP scope",
                    ["modules"] = new JsonArray("MBank.Api", "MCP")
                },
                ["limit"] = 4
            }
        });
        var contextAfterResolutionContent = GetStructuredContent(contextAfterResolution);
        Assert.DoesNotContain(contextAfterResolutionContent["strongMatches"]!.AsArray(), x =>
            x!["id"]!.GetValue<Guid>() == warningId);
        Assert.DoesNotContain(contextAfterResolutionContent["attentionItems"]!.AsArray(), x =>
            x!["kind"]!.GetValue<string>() == "conflict");
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
        return callResult;
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
        var builder = new StringBuilder();
        using var reader = new StringReader(body);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                builder.AppendLine(line["data:".Length..].TrimStart());
            }
        }

        var data = builder.ToString();
        Assert.False(string.IsNullOrWhiteSpace(data), body);
        return data;
    }

    private static string ResolveToolName(JsonObject toolsResponse, string methodName)
    {
        var tools = toolsResponse["result"]!["tools"]!.AsArray();
        var snakeCaseName = ToSnakeCase(methodName);
        var match = tools
            .Select(x => x!["name"]!.GetValue<string>())
            .SingleOrDefault(x =>
                string.Equals(x, methodName, StringComparison.Ordinal)
                || string.Equals(x, snakeCaseName, StringComparison.Ordinal)
                || x.EndsWith($"_{methodName}", StringComparison.Ordinal)
                || x.EndsWith($".{methodName}", StringComparison.Ordinal)
                || x.EndsWith($"_{snakeCaseName}", StringComparison.Ordinal)
                || x.EndsWith($".{snakeCaseName}", StringComparison.Ordinal));

        Assert.True(match is not null, $"MCP tool for {methodName} was not advertised. Tools: {string.Join(", ", tools.Select(x => x!["name"]!.GetValue<string>()))}");
        return match;
    }

    private static JsonObject ResolveTool(JsonObject toolsResponse, string toolName)
    {
        var tools = toolsResponse["result"]!["tools"]!.AsArray();
        return tools
            .Select(x => x!.AsObject())
            .Single(x => x["name"]!.GetValue<string>() == toolName);
    }

    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[i]));
        }

        return builder.ToString();
    }
}
