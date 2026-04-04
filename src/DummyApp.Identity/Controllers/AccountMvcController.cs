using DummyApp.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DummyApp.Identity.Controllers;

public class AccountMvcController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountMvcController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet("/account/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        var model = new LoginViewModel { ReturnUrl = returnUrl };
        // Use explicit view path because controller name is AccountMvcController and view lives under Views/Account/
        return View("~/Views/Account/Login.cshtml", model);
    }

    [HttpPost("/account/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Views/Account/Login.cshtml", model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View("~/Views/Account/Login.cshtml", model);
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View("~/Views/Account/Login.cshtml", model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/account/logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
