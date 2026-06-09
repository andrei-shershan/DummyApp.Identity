using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;
using DummyApp.Identity.Models;

namespace DummyApp.Identity.Controllers
{
    [ApiController]
    public class AuthorizationController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthorizationController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // ─── Authorization endpoint ─────────────────────────────────────────────
        // Browser is redirected here by the BFF when it starts the OIDC flow.
        // If the user is not yet logged-in we redirect to the login page; once
        // they are, we sign in with the OpenIddict scheme which issues the code.
        [AllowAnonymous]
        [HttpGet("~/connect/authorize")]
        [HttpPost("~/connect/authorize")]
        public async Task<IActionResult> Authorize()
        {
            var request = HttpContext.Features.Get<OpenIddictServerAspNetCoreFeature>()?.Transaction?.Request
                ?? throw new InvalidOperationException("The OpenIddict server request cannot be retrieved.");

            // Check whether the user is already logged in via the Identity cookie.
            var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (!result.Succeeded)
            {
                // Not logged in – build the full return URL (the authorize request) and
                // redirect to the login page explicitly.
                // We use Redirect() rather than Challenge() so that OpenIddict's passthrough
                // middleware does not intercept the response before the cookie handler runs.
                var returnUrl = Request.PathBase + Request.Path +
                    QueryString.Create(Request.HasFormContentType
                        ? Request.Form.ToList()
                        : Request.Query.ToList());

                var loginUrl = "/account/login?ReturnUrl=" + Uri.EscapeDataString(returnUrl);
                return Redirect(loginUrl);
            }

            var user = await _userManager.GetUserAsync(result.Principal)
                ?? throw new InvalidOperationException("The user details cannot be retrieved.");

            var identity = new ClaimsIdentity(
                authenticationType: "Bearer",
                nameType: Claims.Name,
                roleType: Claims.Role);

            identity.SetClaim(Claims.Subject, await _userManager.GetUserIdAsync(user))
                    .SetClaim(Claims.Email, await _userManager.GetEmailAsync(user))
                    .SetClaim(Claims.Name, await _userManager.GetUserNameAsync(user) ?? user.Email);

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                identity.AddClaim(Claims.Role, role);
            }

            identity.SetScopes(request.GetScopes());

            // Emit email + name in both access and identity tokens so the BFF
            // (and ApiGateway) can read user info without calling UserInfo endpoint.
            identity.SetDestinations(claim => claim.Type switch
            {
                Claims.Name or Claims.Email or Claims.Subject or Claims.Role
                    => new[] { Destinations.AccessToken, Destinations.IdentityToken },
                _ => new[] { Destinations.AccessToken }
            });

            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // ─── Token endpoint ──────────────────────────────────────────────────────
        [HttpPost("~/connect/token")]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.Features.Get<OpenIddictServerAspNetCoreFeature>()?.Transaction?.Request
                ?? throw new InvalidOperationException("The OpenIddict server request cannot be retrieved.");

            // ── Authorization Code / Refresh Token ──────────────────────────────
            if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
            {
                // Retrieve the principal stored by OpenIddict in the code / refresh token.
                var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                var user = await _userManager.FindByIdAsync(
                    result.Principal?.GetClaim(Claims.Subject) ?? string.Empty);

                if (user is null)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "User no longer exists."
                        }));
                }

                var identity = new ClaimsIdentity(
                    claims: result.Principal!.Claims,
                    authenticationType: "Bearer",
                    nameType: Claims.Name,
                    roleType: Claims.Role);

                identity.SetDestinations(claim => claim.Type switch
                {
                    Claims.Name or Claims.Email or Claims.Subject or Claims.Role
                        => new[] { Destinations.AccessToken, Destinations.IdentityToken },
                    _ => new[] { Destinations.AccessToken }
                });

                var principal = new ClaimsPrincipal(identity);
                var scopes = request.GetScopes();
                if (scopes.Contains("storage.read") || scopes.Contains("storage.write"))
                {
                    principal.SetAudiences("DummyApp.StorageService");
                }

                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            // ── Client Credentials (backend-to-backend) ─────────────────────────
            if (request.IsClientCredentialsGrantType())
            {
                var clientId = request.ClientId ?? throw new InvalidOperationException("Client ID is missing.");
                var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                identity.AddClaim(Claims.Subject, clientId, Destinations.AccessToken);

                var scopes = request.GetScopes();
                foreach (var scope in scopes)
                {
                    identity.AddClaim(Claims.Private.Scope, scope, Destinations.AccessToken);
                }

                var principal = new ClaimsPrincipal(identity);
                principal.SetScopes(scopes);
                if (scopes.Contains("storage.read") || scopes.Contains("storage.write"))
                {
                    principal.SetAudiences("DummyApp.StorageService");
                }

                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return BadRequest(new OpenIddictResponse
            {
                Error = Errors.UnsupportedGrantType,
                ErrorDescription = "The specified grant type is not supported."
            });
        }

        // ─── Logout endpoint ─────────────────────────────────────────────────────
        // Called by the BFF's SignOutAsync("oidc") via the end_session_endpoint in
        // the discovery document. Signs the user out of Identity and redirects to
        // post_logout_redirect_uri (the BFF signout-callback).
        [AllowAnonymous]
        [HttpGet("~/connect/logout")]
        [HttpPost("~/connect/logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

            return SignOut(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties { RedirectUri = "/" });
        }
    }
}
