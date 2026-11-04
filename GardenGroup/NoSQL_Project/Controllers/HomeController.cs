using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using NoSQL_Project.Models;
using NoSQL_Project.Repositories.Interfaces;
using NoSQL_Project.ViewModels;

namespace NoSQL_Project.Controllers;

// The dashboard every signed-in employee sees first.
public class HomeController : Controller
{
    private readonly ITicketRepository _tickets;

    public HomeController(ITicketRepository tickets)
    {
        _tickets = tickets;
    }

    public async Task<IActionResult> Index()
    {
        // Service desk employees see counts for everyone. Regular employees only see their own tickets.
        ObjectId? reporterFilter = null;
        if (!User.IsInRole(Roles.ServiceDesk))
        {
            reporterFilter = GetCurrentEmployeeId();
        }

        var counts = await _tickets.GetStatusCounts(reporterFilter);
        var recentTickets = await _tickets.Find(reporterFilter);

        var model = new Dashboard
        {
            Open = counts.FirstOrDefault(c => c.Status == "Open")?.Count ?? 0,
            Resolved = counts.FirstOrDefault(c => c.Status == "Resolved")?.Count ?? 0,
            Closed = counts.FirstOrDefault(c => c.Status == "Closed")?.Count ?? 0,
            Recent = recentTickets.Take(5).ToList()
        };

        return View(model);
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = 500;
        return View();
    }

    // Reads the logged-in employee's own ID out of their login cookie.
    // Guards against a missing/invalid claim instead of letting ObjectId.Parse crash.
    private ObjectId GetCurrentEmployeeId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(rawId) || !ObjectId.TryParse(rawId, out var id))
        {
            throw new InvalidOperationException("Signed-in employee is missing a valid identifier claim.");
        }

        return id;
    }
}
