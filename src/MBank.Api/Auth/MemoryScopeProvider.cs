namespace MBank.Api.Auth;

public sealed class MemoryScopeProvider
{
    public AuthenticatedMemoryScope? Current { get; private set; }

    public void Set(AuthenticatedMemoryScope scope)
    {
        Current = scope;
    }
}
