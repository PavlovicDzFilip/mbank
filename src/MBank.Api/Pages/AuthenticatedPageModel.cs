using System.Security.Claims;
using MBank.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MBank.Api.Pages;

[Authorize]
public abstract class AuthenticatedPageModel : PageModel
{
    protected bool TryGetUserId(out UserId userId)
    {
        userId = default;
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(rawId, out var value))
        {
            return false;
        }

        userId = new UserId(value);
        return true;
    }

}
