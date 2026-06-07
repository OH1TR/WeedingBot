using Microsoft.AspNetCore.Mvc;

namespace BotMapTool.Controllers;

/// <summary>
/// Proxies map requests from Leaflet to Maanmittauslaitos avoin karttakuvapalvelu,
/// adding the API key authentication header on the way.
/// </summary>
[ApiController]
public class MmlProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MmlProxyController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("mml/{**path}")]
    public async Task Get(string path)
    {
        var client = _httpClientFactory.CreateClient(Configuration.MmlSettings.HttpClientName);
        var ct = HttpContext.RequestAborted;

        // Forward the path and query string as-is (e.g. wmts/1.0.0/.../{z}/{y}/{x}.png).
        using var upstream = await client.GetAsync(
            path + Request.QueryString.Value, HttpCompletionOption.ResponseHeadersRead, ct);

        Response.StatusCode = (int)upstream.StatusCode;
        Response.ContentType = upstream.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        await upstream.Content.CopyToAsync(Response.Body, ct);
    }
}
