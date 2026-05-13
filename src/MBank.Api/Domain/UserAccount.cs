namespace MBank.Api.Domain;

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = "";

    public string? Email { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
