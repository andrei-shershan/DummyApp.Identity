using DummyApp.Identity.Constants;
using DummyApp.Identity.Configuration;
using DummyApp.Identity.Models;
using DummyApp.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DummyApp.Identity.Controllers;

public class AccountMvcController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IInviteService _inviteService;
    private readonly AppSettings _appSettings;

    public AccountMvcController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IInviteService inviteService,
        IOptions<AppSettings> appSettings)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _inviteService = inviteService;
        _appSettings = appSettings.Value;
    }

    [HttpGet("/account/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        var model = new LoginViewModel { ReturnUrl = returnUrl };
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

    [HttpGet("/account/register/{token?}")]
    public async Task<IActionResult> Register(string? token, string? returnUrl = null)
    {
        var model = new RegisterViewModel { Token = token ?? string.Empty, ReturnUrl = returnUrl };
        if (string.IsNullOrWhiteSpace(token))
        {
            model.InviteValid = false;
            ModelState.AddModelError(string.Empty, "Приглашение отсутствует или недействительно.");
            return View("~/Views/Account/Register.cshtml", model);
        }

        var invite = await _inviteService.GetInviteByTokenAsync(token.Trim(), CancellationToken.None);
        if (invite == null)
        {
            model.InviteValid = false;
            ModelState.AddModelError(string.Empty, "Приглашение недействительно.");
            return View("~/Views/Account/Register.cshtml", model);
        }

        if (invite.ExpiresAt < DateTime.UtcNow)
        {
            model.InviteValid = false;
            ModelState.AddModelError(string.Empty, "Срок действия приглашения истёк.");
            return View("~/Views/Account/Register.cshtml", model);
        }

        return View("~/Views/Account/Register.cshtml", model);
    }

    [HttpPost("/account/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Views/Account/Register.cshtml", model);

        if (string.IsNullOrWhiteSpace(model.Token))
        {
            ModelState.AddModelError(string.Empty, "Приглашение отсутствует.");
            model.InviteValid = false;
            return View("~/Views/Account/Register.cshtml", model);
        }

        var invite = await _inviteService.GetInviteByTokenAsync(model.Token.Trim(), CancellationToken.None);
        if (invite == null)
        {
            ModelState.AddModelError(string.Empty, "Приглашение недействительно.");
            model.InviteValid = false;
            return View("~/Views/Account/Register.cshtml", model);
        }

        if (invite.ExpiresAt < DateTime.UtcNow)
        {
            ModelState.AddModelError(string.Empty, "Срок действия приглашения истёк.");
            model.InviteValid = false;
            return View("~/Views/Account/Register.cshtml", model);
        }

        if (!string.Equals(invite.Email, model.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Email не совпадает с адресом, указанным в приглашении.");
            model.InviteValid = true;
            return View("~/Views/Account/Register.cshtml", model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(string.Empty, "Пользователь с таким email уже существует.");
            model.InviteValid = true;
            return View("~/Views/Account/Register.cshtml", model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            model.InviteValid = true;
            return View("~/Views/Account/Register.cshtml", model);
        }

        if (!await _roleManager.RoleExistsAsync(RoleNames.Creator))
        {
            await _roleManager.CreateAsync(new IdentityRole(RoleNames.Creator));
        }

        await _userManager.AddToRoleAsync(user, RoleNames.Creator);
        await _inviteService.RemoveInviteAsync(model.Token.Trim(), CancellationToken.None);

        await _signInManager.SignInAsync(user, isPersistent: false);

        if (IsReturnUrlAllowed(model.ReturnUrl))
        {
            var bffBaseUrl = _appSettings.Services.BFF.BaseUrl.TrimEnd('/');
            var loginUrl = $"{bffBaseUrl}/login?returnUrl={Uri.EscapeDataString(model.ReturnUrl!)}";
            return Redirect(loginUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/account/logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    private bool IsReturnUrlAllowed(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        var frontendBaseUrl = _appSettings.Services.Frontend?.BaseUrl?.TrimEnd('/');
        if (!string.IsNullOrEmpty(frontendBaseUrl) && returnUrl.StartsWith(frontendBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
