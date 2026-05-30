using System.Text.Json;

namespace MBank.Api.Mcp;

public sealed class McpJsonRpcErrorMiddleware(
    RequestDelegate next,
    ILogger<McpJsonRpcErrorMiddleware> logger)
{
    private const string ParseErrorJson = """
        {"jsonrpc":"2.0","id":null,"error":{"code":-32700,"message":"Parse error"}}
        """;

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next(httpContext);
        }
        catch (JsonException exception) when (!httpContext.Response.HasStarted)
        {
            logger.LogWarning(exception, "Rejected malformed JSON-RPC request body.");

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync(ParseErrorJson, httpContext.RequestAborted);
        }
    }
}
