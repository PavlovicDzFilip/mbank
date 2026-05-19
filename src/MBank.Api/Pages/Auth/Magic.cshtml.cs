using System.Security.Claims;
using MBank.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MBank.Api.Pages.Auth;

public sealed class MagicModel(MagicLinkAuthenticationService magicLinks) : PageModel
{
    public string Error { get; private set; } = "This sign-in link is invalid or expired.";

    public async Task<IActionResult> OnGetAsync(Guid id, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Page();
        }

        var user = await magicLinks.ConsumeLoginLinkAsync(id, token, cancellationToken);
        if (user is null)
        {
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email ?? user.Name),
            new("email", user.Email ?? user.Name)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToPage("/Login", new { status = "signed-in" });
    }
}
