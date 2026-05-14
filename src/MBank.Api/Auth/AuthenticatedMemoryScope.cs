using MBank.Api.Domain;

namespace MBank.Api.Auth;

public sealed record AuthenticatedMemoryScope(
    UserId UserId,
    ProjectId ProjectId,
    AccessTokenPermissions Permissions);
