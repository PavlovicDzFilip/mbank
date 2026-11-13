using MBank.Api.Options;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;

namespace MBank.Api.Services;

public sealed class ServerUrls(
    IOptions<MagicLinkOptions> options,
    IHttpContextAccessor httpContextAccessor)
{
    public string GetUrl(PathString path)
    {
        var publicBaseUrl = options.Value.PublicBaseUrl;
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return publicBaseUrl.TrimEnd('/') + path.ToUriComponent();
        }

        var request = httpContextAccessor.HttpContext?.Request;
        return request is null
            ? path.ToUriComponent()
            : UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, path);
    }
}
