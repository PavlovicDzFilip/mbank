using System.Net;
using MBank.Api.Data;
using MBank.Api.Domain;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace MBank.Api.Services;

public sealed class MagicLinkAuthenticationService(
    MemoryDbContext db,
    IEmailSender emailSender)
{
    public async Task RequestLoginLinkAsync(
        string email,
        string callbackBaseUrl,
        CancellationToken cancellationToken)
    {
        var issued = MagicLinkLogin.Create(email);
        db.MagicLinkLogins.Add(issued.Login);
        await db.SaveChangesAsync(cancellationToken);

        var link = QueryHelpers.AddQueryString(
            callbackBaseUrl,
            new Dictionary<string, string?>
            {
                ["id"] = issued.Login.Id.Value.ToString(),
                ["token"] = issued.Secret.Value
            });
        var encodedLink = WebUtility.HtmlEncode(link);
        var encodedEmail = WebUtility.HtmlEncode(issued.Login.Email);

        await emailSender.SendAsync(
            new EmailMessage(
                issued.Login.Email,
                "Sign in to MBank",
                $"Use this link to sign in to MBank: {link}",
                $"""
                <p>Use this link to sign in to MBank:</p>
                <p><a href="{encodedLink}">Sign in to MBank</a></p>
                <p>This link was requested for {encodedEmail} and expires in {MagicLinkLogin.Lifetime.TotalMinutes} minutes.</p>
                """),
            cancellationToken);
    }

    public async Task<UserAccount?> ConsumeLoginLinkAsync(
        Guid id,
        string token,
        CancellationToken cancellationToken)
    {
        var loginId = new MagicLinkLoginId(id);
        var login = await db.MagicLinkLogins
            .FirstOrDefaultAsync(x => x.Id == loginId, cancellationToken);
        if (login is null || !login.Consume(token, DateTimeOffset.UtcNow))
        {
            return null;
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == login.Email, cancellationToken);
        if (user is null)
        {
            user = UserAccount.Create(login.Email, login.Email);
            db.Users.Add(user);
        }

        await db.SaveChangesAsync(cancellationToken);
        return user;
    }
}
