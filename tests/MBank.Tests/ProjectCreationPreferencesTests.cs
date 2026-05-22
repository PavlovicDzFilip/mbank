using MBank.Api.Pages.Projects;
using Microsoft.AspNetCore.Http;

namespace MBank.Tests;

public sealed class ProjectCreationPreferencesTests
{
    [Fact]
    public void Default_is_checked_when_no_cookie_exists()
    {
        var context = new DefaultHttpContext();

        Assert.True(ProjectCreationPreferences.GetSeedStarterPackDefault(context.Request));
    }

    [Fact]
    public void Cookie_restores_last_choice()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{ProjectCreationPreferences.SeedStarterPackCookieName}=false";

        Assert.False(ProjectCreationPreferences.GetSeedStarterPackDefault(context.Request));
    }

    [Fact]
    public void Remember_writes_cookie_for_next_visit()
    {
        var context = new DefaultHttpContext();

        ProjectCreationPreferences.RememberSeedStarterPackChoice(
            context.Request,
            context.Response,
            seedStarterPack: false);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains(ProjectCreationPreferences.SeedStarterPackCookieName, setCookie);
        Assert.Contains("false", setCookie, StringComparison.OrdinalIgnoreCase);
    }
}
