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

public class AccountController(IEmployeeRepository employees, IPasswordHasher<Employee> hasher) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl) => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View(new LoginForm { ReturnUrl = returnUrl });
    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var employee = await employees.GetByEmail(form.Email);
        var result = PasswordVerificationResult.Failed;
        if (employee != null && Roles.All.Contains(employee.Role)) {
            try { result = hasher.VerifyHashedPassword(employee, employee.PasswordHash, form.Password); }
            catch (FormatException) { /* Unsupported legacy hashes require a password reset by service desk. */ }
        }
        if (employee == null || result == PasswordVerificationResult.Failed) {
            ModelState.AddModelError("", "Email or password is incorrect.");
            return View(form);
        }
        if (result == PasswordVerificationResult.SuccessRehashNeeded) {
            employee.PasswordHash = hasher.HashPassword(employee, form.Password);
            await employees.Update(employee.Id, employee);
        }
        var identity = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, employee.Id.ToString()),
            new Claim(ClaimTypes.Name, employee.DisplayName),
            new Claim(ClaimTypes.Role, employee.Role),
            new Claim("passwordVersion", employee.PasswordHash)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Url.IsLocalUrl(form.ReturnUrl) ? LocalRedirect(form.ReturnUrl!) : RedirectToAction("Index", "Home");
    }
    [HttpPost] public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(); return RedirectToAction(nameof(Login)); }
    [AllowAnonymous] public IActionResult Denied() { Response.StatusCode = 403; return View(); }
}
