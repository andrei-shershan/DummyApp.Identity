using DummyApp.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DummyApp.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BffProxyController : ControllerBase
{
    private readonly ITokenStore _tokenStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BffProxyController> _logger;

    public BffProxyController(ITokenStore tokenStore, IHttpClientFactory httpClientFactory, ILogger<BffProxyController> logger)
    {
        _tokenStore = tokenStore;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
            return Unauthorized();

        var tokens = await _tokenStore.GetAsync(userId);
        return Ok(new { user = User?.Identity?.Name, hasToken = tokens != null });
    }
}
