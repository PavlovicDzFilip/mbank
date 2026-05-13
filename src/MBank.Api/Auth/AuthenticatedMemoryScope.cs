using MBank.Api.Domain;

namespace MBank.Api.Auth;

public sealed record AuthenticatedMemoryScope(
    Guid UserId,
    Guid ProjectId,
    AccessTokenPermissions Permissions);
