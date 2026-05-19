using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using MBank.Api.Data;
using MBank.Api.Domain;
using MBank.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MBank.Tests;

public sealed class ProjectWorkspaceTests(MBankWebApplicationFactory factory)
    : IClassFixture<MBankWebApplicationFactory>, IAsyncLifetime
{
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
    public async Task Signed_in_user_can_create_list_rename_and_delete_projects()
    {
        var client = await CreateSignedInClientAsync("owner@example.test");

        var create = await PostAntiforgeryFormAsync(
            client,
            "/projects",
            new Dictionary<string, string> { ["Name"] = "Alpha" },
            "Create");
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var projectId = ExtractProjectId(create.Headers.Location!.OriginalString);

        var list = await client.GetAsync("/projects");
        var listBody = await list.Content.ReadAsStringAsync();
        Assert.Contains("Alpha", listBody);

        var rename = await PostAntiforgeryFormAsync(
            client,
            $"/projects/{projectId}",
            new Dictionary<string, string> { ["Name"] = "Alpha Renamed" },
            "Rename");
        Assert.Equal(HttpStatusCode.Redirect, rename.StatusCode);

        var detail = await client.GetAsync($"/projects/{projectId}");
        var detailBody = await detail.Content.ReadAsStringAsync();
        Assert.Contains("Alpha Renamed", detailBody);

        var delete = await PostAntiforgeryFormAsync(
            client,
            $"/projects/{projectId}",
            new Dictionary<string, string>(),
            "Delete");
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);
        Assert.Equal("/projects", delete.Headers.Location?.ToString());

        var afterDelete = await client.GetAsync($"/projects/{projectId}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Project_names_are_not_globally_unique()
    {
        var first = await CreateSignedInClientAsync("first@example.test");
        var second = await CreateSignedInClientAsync("second@example.test");

        var firstCreate = await PostAntiforgeryFormAsync(
            first,
            "/projects",
            new Dictionary<string, string> { ["Name"] = "Shared Name" },
            "Create");
        Assert.Equal(HttpStatusCode.Redirect, firstCreate.StatusCode);

        var secondCreate = await PostAntiforgeryFormAsync(
            second,
            "/projects",
            new Dictionary<string, string> { ["Name"] = "Shared Name" },
            "Create");
        Assert.Equal(HttpStatusCode.Redirect, secondCreate.StatusCode);
    }

    [Fact]
    public async Task User_can_create_and_revoke_project_token_and_secret_is_shown_once()
    {
        var client = await CreateSignedInClientAsync("token.user@example.test");
        var createProject = await PostAntiforgeryFormAsync(
            client,
            "/projects",
            new Dictionary<string, string> { ["Name"] = "Token Project" },
            "Create");
        var projectId = ExtractProjectId(createProject.Headers.Location!.OriginalString);

        var createToken = await PostAntiforgeryFormAsync(
            client,
            $"/projects/{projectId}",
            new Dictionary<string, string> { ["TokenName"] = "Codex repo token" },
            "CreateToken");
        Assert.Equal(HttpStatusCode.Redirect, createToken.StatusCode);

        var issued = await client.GetAsync(createToken.Headers.Location!);
        var issuedBody = await issued.Content.ReadAsStringAsync();
        Assert.Contains("mbp_", issuedBody);
        Assert.Contains("Copy this token now.", issuedBody);

        var afterIssue = await client.GetAsync($"/projects/{projectId}");
        var afterIssueBody = await afterIssue.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Copy this token now.", afterIssueBody);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == "token.user@example.test");
            var token = await db.AccessTokens.SingleAsync(x => x.UserId == user.Id);
            Assert.Null(token.RevokedAt);
        }

        var revoke = await PostAntiforgeryFormAsync(
            client,
            $"/projects/{projectId}",
            new Dictionary<string, string> { ["tokenId"] = await GetTokenIdAsync("token.user@example.test", projectId) },
            "RevokeToken");
        Assert.Equal(HttpStatusCode.Redirect, revoke.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == "token.user@example.test");
            var token = await db.AccessTokens.SingleAsync(x => x.UserId == user.Id);
            Assert.NotNull(token.RevokedAt);
        }
    }

    [Fact]
    public async Task Home_redirects_signed_in_users_to_projects()
    {
        var client = await CreateSignedInClientAsync("home.redirect@example.test");

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/projects", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Projects_page_requires_sign_in()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/projects");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString(), StringComparison.Ordinal);
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
        Assert.Equal("/projects", callback.Headers.Location?.ToString());
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

    private async Task<string> GetTokenIdAsync(string email, Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        var token = await db.AccessTokens.SingleAsync(
            x => x.UserId == user.Id && x.ProjectId == new ProjectId(projectId));
        return token.Id.Value.ToString();
    }
}
