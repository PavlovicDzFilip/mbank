using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using MBank.Api.Data;
using MBank.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MBank.Tests;

public sealed class MagicLinkLoginTests(MBankWebApplicationFactory factory)
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
    public async Task Login_page_sends_magic_link_email()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await RequestMagicLinkAsync(client, "New.User@example.test");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login?status=sent", response.Headers.Location?.ToString());

        var emails = factory.Services.GetRequiredService<FakeEmailSender>().Messages;
        var email = Assert.Single(emails);
        Assert.Equal("new.user@example.test", email.To);
        Assert.Contains("/auth/magic", email.TextBody);
    }

    [Fact]
    public async Task Magic_link_creates_user_and_signs_in_once()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await RequestMagicLinkAsync(client, "First.Login@example.test");
        var link = ExtractMagicLink(factory.Services.GetRequiredService<FakeEmailSender>().Messages.Single());

        var callback = await client.GetAsync(link.PathAndQuery);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/login?status=signed-in", callback.Headers.Location?.ToString());
        Assert.Contains(
            callback.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("MBank.Auth=", StringComparison.Ordinal));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == "first.login@example.test");
            Assert.Equal("first.login@example.test", user.Name);
        }

        var replay = await client.GetAsync(link.PathAndQuery);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayBody = await replay.Content.ReadAsStringAsync();
        Assert.Contains("invalid or expired", replayBody);
    }

    [Fact]
    public async Task Login_page_rejects_invalid_email()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await RequestMagicLinkAsync(client, "not-an-email");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Enter a valid email address.", body);
        Assert.Empty(factory.Services.GetRequiredService<FakeEmailSender>().Messages);
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

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, "Could not find antiforgery token in login page.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    private static Uri ExtractMagicLink(EmailMessage message)
    {
        var match = Regex.Match(message.TextBody, @"https?://\S+", RegexOptions.CultureInvariant);
        Assert.True(match.Success, "Could not find magic link in email body.");
        return new Uri(match.Value);
    }
}
