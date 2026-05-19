using System.ComponentModel.DataAnnotations;
using MBank.Api.Options;
using MBank.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace MBank.Api.Pages;

public sealed class LoginModel(
    MagicLinkAuthenticationService magicLinks,
    IOptions<MagicLinkOptions> options) : PageModel
{
    [BindProperty]
    [Required]
    [EmailAddress]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    public string? Message { get; private set; }

    public string? Error { get; private set; }

    public bool IsSignedIn => User.Identity?.IsAuthenticated == true;

    public string? SignedInEmail => User.FindFirst("email")?.Value;

    public IActionResult OnGet(string? status = null)
    {
        if (IsSignedIn)
        {
            return RedirectToPage("/Projects/Index");
        }

        Message = status switch
        {
            "sent" => "Check your email for a sign-in link.",
            _ => null
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Error = "Enter a valid email address.";
            return Page();
        }

        try
        {
            await magicLinks.RequestLoginLinkAsync(
                Email,
                BuildCallbackBaseUrl(),
                cancellationToken);
        }
        catch (ArgumentException)
        {
            Error = "Enter a valid email address.";
            return Page();
        }

        return RedirectToPage("/Login", new { status = "sent" });
    }

    private string BuildCallbackBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.PublicBaseUrl))
        {
            return new Uri(new Uri(options.Value.PublicBaseUrl.TrimEnd('/') + "/"), "auth/magic").ToString();
        }

        return Url.PageLink("/Auth/Magic")
            ?? throw new InvalidOperationException("Could not generate magic-link callback URL.");
    }
}
