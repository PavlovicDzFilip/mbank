using MBank.Api.Domain;

namespace MBank.Tests;

public sealed class UserAccountTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Create_requires_valid_email(string email)
    {
        Assert.Throws<ArgumentException>(() => UserAccount.Create("Test User", email));
    }

    [Fact]
    public void Create_normalizes_email()
    {
        var user = UserAccount.Create("Test User", "User.Name@Example.Test");

        Assert.Equal("user.name@example.test", user.Email);
    }

    [Fact]
    public void Create_generates_user_id()
    {
        var user = UserAccount.Create("Test User", "test@example.test");

        Assert.NotEqual(Guid.Empty, user.Id.Value);
    }
}
