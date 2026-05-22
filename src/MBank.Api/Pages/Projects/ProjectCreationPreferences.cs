using Microsoft.AspNetCore.Http;

namespace MBank.Api.Pages.Projects;

public static class ProjectCreationPreferences
{
    public const string SeedStarterPackCookieName = "MBank.SeedStarterPack";

    public static bool GetSeedStarterPackDefault(HttpRequest request)
    {
        if (request.Cookies.TryGetValue(SeedStarterPackCookieName, out var value)
            && bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return true;
    }

    public static void RememberSeedStarterPackChoice(HttpRequest request, HttpResponse response, bool seedStarterPack)
    {
        response.Cookies.Append(
            SeedStarterPackCookieName,
            seedStarterPack.ToString().ToLowerInvariant(),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(365)
            });
    }
}
