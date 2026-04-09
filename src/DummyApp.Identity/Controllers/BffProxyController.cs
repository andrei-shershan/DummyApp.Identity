using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DummyApp.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BffProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BffProxyController> _logger;

    public BffProxyController(IHttpClientFactory httpClientFactory, ILogger<BffProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new { user = User?.Identity?.Name });
    }
}
