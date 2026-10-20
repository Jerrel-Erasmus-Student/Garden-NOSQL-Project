using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using NoSQL_Project.Models;
using NoSQL_Project.Repositories.Interfaces;
using NoSQL_Project.ViewModels;
namespace NoSQL_Project.Controllers;

public class HomeController(ITicketRepository tickets) : Controller
{
    public async Task<IActionResult> Index()
    {
        ObjectId? reporter = User.IsInRole(Roles.ServiceDesk) ? null : ObjectId.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var counts = await tickets.GetStatusCounts(reporter);
        var model = new Dashboard {
            Open = counts.FirstOrDefault(c => c.Status == "Open")?.Count ?? 0,
            Resolved = counts.FirstOrDefault(c => c.Status == "Resolved")?.Count ?? 0,
            Closed = counts.FirstOrDefault(c => c.Status == "Closed")?.Count ?? 0,
            Recent = (await tickets.Find(reporter)).Take(5).ToList()
        };
        return View(model);
    }
    [AllowAnonymous, IgnoreAntiforgeryToken, ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() { Response.StatusCode = 500; return View(); }
}
