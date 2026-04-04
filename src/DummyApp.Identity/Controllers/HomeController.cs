using Microsoft.AspNetCore.Mvc;

namespace DummyApp.Identity.Controllers;

public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return View();
    }
}
