namespace MBank.Api.Auth;

public sealed class CurrentMemoryScope
{
    public AuthenticatedMemoryScope? Current { get; set; }
}
