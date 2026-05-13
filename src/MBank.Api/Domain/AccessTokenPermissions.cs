using System.Text.Json.Serialization;

namespace MBank.Api.Domain;

[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<AccessTokenPermissions>))]
public enum AccessTokenPermissions
{
    None = 0,
    Create = 1,
    Read = 2,
    Update = 4,
    Delete = 8,
    Crud = Create | Read | Update | Delete
}
