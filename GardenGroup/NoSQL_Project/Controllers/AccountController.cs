using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NoSQL_Project.Models;
using NoSQL_Project.Repositories.Interfaces;
using NoSQL_Project.ViewModels;

namespace NoSQL_Project.Controllers;

// Handles signing in, signing out, and the "access denied" page.
public class AccountController : Controller
{
    private readonly IEmployeeRepository _employees;
    private readonly IPasswordHasher<Employee> _hasher;

    public AccountController(IEmployeeRepository employees, IPasswordHasher<Employee> hasher)
    {
        _employees = employees;
        _hasher = hasher;
    }

    // Shows the login form. If the user is already signed in, send them to the dashboard instead.
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginForm { ReturnUrl = returnUrl });
    }

    // Checks the submitted email/password and signs the employee in with a cookie.
    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginForm form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var employee = await _employees.GetByEmail(form.Email);
        var passwordResult = PasswordVerificationResult.Failed;

        if (employee != null && Roles.All.Contains(employee.Role))
        {
            try
            {
                passwordResult = _hasher.VerifyHashedPassword(employee, employee.PasswordHash, form.Password);
            }
            catch (FormatException)
            {
                // Some imported accounts have a legacy hash format. Treat them as "wrong password"
                // and tell the employee to ask the service desk for a reset, rather than crashing.
            }
        }

        if (employee == null || passwordResult == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "Email or password is incorrect.");
            return View(form);
        }

        // The hasher sometimes wants to upgrade an old hash to a stronger one. Save that upgrade now.
        if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            employee.PasswordHash = _hasher.HashPassword(employee, form.Password);
            await _employees.Update(employee.Id, employee);
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, employee.Id.ToString()),
            new Claim(ClaimTypes.Name, employee.DisplayName),
            new Claim(ClaimTypes.Role, employee.Role),
            new Claim("passwordVersion", employee.PasswordHash)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        if (!string.IsNullOrEmpty(form.ReturnUrl) && Url.IsLocalUrl(form.ReturnUrl))
        {
            return LocalRedirect(form.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // Shown when a Regular employee tries to open a Service Desk-only page.
    [AllowAnonymous]
    public IActionResult Denied()
    {
        Response.StatusCode = 403;
        return View();
    }
}
