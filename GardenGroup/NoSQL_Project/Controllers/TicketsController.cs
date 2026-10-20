using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MongoDB.Bson;
using MongoDB.Driver;
using NoSQL_Project.Models;
using NoSQL_Project.Repositories.Interfaces;
using NoSQL_Project.Services;
using NoSQL_Project.ViewModels;
namespace NoSQL_Project.Controllers;

public class TicketsController(ITicketRepository tickets, IEmployeeRepository employees, TicketSearchService search, PrioritySortService priorities, TicketTransferService transfer) : Controller
{
    private ObjectId CurrentId => ObjectId.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
    private bool IsDesk => User.IsInRole(Roles.ServiceDesk);
    public async Task<IActionResult> Index(string? q, string mode = "AND", string sort = "newest", int page = 1, bool archived = false)
    {
        q = q?.Trim() ?? "";
        if (q.Length > 160 || q.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > 10 ||
            !new[] { "AND", "OR" }.Contains(mode) || !new[] { "newest", "priority-high", "priority-low" }.Contains(sort)) return BadRequest("Invalid search. Use at most 10 words and 160 characters.");
        var statusFilter = archived
            ? Builders<Ticket>.Filter.In(t => t.Dates.Status, new[] { TicketOptions.Resolved, TicketOptions.Closed })
            : Builders<Ticket>.Filter.Eq(t => t.Dates.Status, TicketOptions.Open);
        var found = await tickets.Find(IsDesk ? null : CurrentId, search.BuildFilter(q, mode) & statusFilter);
        var ordered = sort == "newest" ? found : priorities.Sort(found, sort);
        var model = new TicketList { Query = q, Mode = mode, Sort = sort, Total = found.Count, Archived = archived };
        model.Page = Math.Clamp(page, 1, model.Pages);
        model.Tickets = ordered.Skip((model.Page - 1) * 20).Take(20).ToList();
        if (IsDesk) model.EmployeeNames = (await employees.GetAll()).ToDictionary(e => e.Id.ToString(), e => e.DisplayName);
        return View(model);
    }
    public async Task<IActionResult> Details(string id)
    {
        if (!ObjectId.TryParse(id, out var oid)) return NotFound();
        var ticket = await tickets.GetById(oid);
        if (ticket == null || (!IsDesk && ticket.ReportedByEmployeeId != CurrentId)) return NotFound();
        var reporter = await employees.GetById(ticket.ReportedByEmployeeId);
        var assignee = ticket.AssignedToEmployeeId.HasValue ? await employees.GetById(ticket.AssignedToEmployeeId.Value) : null;
        return View(new TicketDetails { Ticket = ticket, Reporter = reporter?.DisplayName ?? "Unknown employee", Assignee = assignee?.DisplayName ?? "Unassigned",
            TransferRecipients = IsDesk ? (await employees.GetAll()).Where(e => e.Role == Roles.ServiceDesk && e.Id != ticket.AssignedToEmployeeId).Select(e => new SelectListItem(e.DisplayName, e.Id.ToString())).ToList() : [] });
    }
    [HttpGet] public async Task<IActionResult> Create() => View("Form", await Populate(new TicketForm { ReporterId = CurrentId.ToString() }));
    [HttpPost]
    public async Task<IActionResult> Create(TicketForm form)
    {
        form.Id = null;
        // New incidents always start open; regular employees cannot choose another reporter or assignee.
        form.Status = TicketOptions.Open;
        ModelState.Remove(nameof(form.Status));
        await Validate(form);
        if (!ModelState.IsValid) return View("Form", await Populate(form));
        var ticket = new Ticket { Id = ObjectId.GenerateNewId() };
        Apply(form, ticket);
        await tickets.Create(ticket);
        TempData["Success"] = "Ticket created. The service desk can now help you.";
        return RedirectToAction(nameof(Details), new { id = ticket.Id.ToString() });
    }
    [Authorize(Roles = Roles.ServiceDesk), HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (!ObjectId.TryParse(id, out var oid)) return NotFound();
        var ticket = await tickets.GetById(oid);
        if (ticket == null) return NotFound();
        return View("Form", await Populate(new TicketForm { Id = id, Subject = ticket.Subject, Description = ticket.Description,
            Priority = TicketOptions.Priorities.Contains(ticket.Priority) ? ticket.Priority : "Normal",
            Type = TicketOptions.Types.Contains(ticket.Type) ? ticket.Type : "Other", Status = ticket.Status,
            EventDate = ticket.Dates.DateReported.Date, Deadline = ticket.Deadline == default ? DateTime.UtcNow.Date.AddDays(7) : ticket.Deadline.Date,
            ReporterId = ticket.ReportedByEmployeeId.ToString(), AssigneeId = ticket.AssignedToEmployeeId?.ToString() }));
    }
    [Authorize(Roles = Roles.ServiceDesk), HttpPost]
    public async Task<IActionResult> Edit(string id, TicketForm form)
    {
        if (!ObjectId.TryParse(id, out var oid)) return NotFound();
        var ticket = await tickets.GetById(oid);
        if (ticket == null) return NotFound();
        form.Id = id;
        await Validate(form);
        if (!ModelState.IsValid) return View("Form", await Populate(form));
        Apply(form, ticket);
        if (!await tickets.Update(oid, ticket)) return NotFound();
        TempData["Success"] = "Ticket updated.";
        return RedirectToAction(nameof(Details), new { id });
    }
    [Authorize(Roles = Roles.ServiceDesk), HttpPost]
    public async Task<IActionResult> Close(string id)
    {
        if (!ObjectId.TryParse(id, out var oid)) return NotFound();
        var ticket = await tickets.GetById(oid);
        if (ticket == null) return NotFound();
        TicketWorkflow.SetStatus(ticket, TicketOptions.Closed, DateTime.UtcNow);
        if (!await tickets.Update(oid, ticket)) return NotFound();
        TempData["Success"] = "Ticket closed. Its resolution outcome has been retained.";
        return RedirectToAction(nameof(Details), new { id });
    }
    [Authorize(Roles = Roles.ServiceDesk), HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        if (!ObjectId.TryParse(id, out var oid)) return NotFound();
        var ticket = await tickets.GetById(oid);
        return ticket == null ? NotFound() : View(ticket);
    }
    [Authorize(Roles = Roles.ServiceDesk), HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(string id, bool confirm)
    {
        if (!ObjectId.TryParse(id, out var oid)) return NotFound();
        var ticket = await tickets.GetById(oid);
        if (ticket == null) return NotFound();
        if (!confirm) { ModelState.AddModelError("", "Confirm permanent deletion, or use Close to retain the incident."); return View("Delete", ticket); }
        if (!await tickets.Delete(oid)) return NotFound();
        TempData["Success"] = "Ticket permanently deleted.";
        return RedirectToAction(nameof(Index));
    }
    [Authorize(Roles = Roles.ServiceDesk), HttpPost]
    public async Task<IActionResult> Transfer(string id, string recipientId)
    {
        if (!ObjectId.TryParse(id, out var ticketId) || !ObjectId.TryParse(recipientId, out var recipient)) return BadRequest("Invalid transfer details.");
        var error = await transfer.Transfer(ticketId, recipient);
        if (error != null) TempData["Error"] = error;
        else TempData["Success"] = "Ticket transferred to the selected service desk employee.";
        return RedirectToAction(nameof(Details), new { id });
    }
    private async Task Validate(TicketForm form)
    {
        if (form.EventDate.HasValue && (form.EventDate.Value.Date > DateTime.UtcNow.Date || form.EventDate.Value.Year < 2000)) ModelState.AddModelError(nameof(form.EventDate), "Choose an event date from 2000 through today (UTC).");
        if (form.Deadline.HasValue && form.EventDate.HasValue && form.Deadline.Value.Date < form.EventDate.Value.Date) ModelState.AddModelError(nameof(form.Deadline), "Deadline cannot be before the event date.");
        if (!IsDesk) {
            form.ReporterId = CurrentId.ToString(); form.AssigneeId = null;
            ModelState.Remove(nameof(form.ReporterId)); ModelState.Remove(nameof(form.AssigneeId));
        }
        if (!ObjectId.TryParse(form.ReporterId, out var reporter) || await employees.GetById(reporter) == null) ModelState.AddModelError(nameof(form.ReporterId), "Choose an existing reporter.");
        if (!string.IsNullOrEmpty(form.AssigneeId)) {
            if (!ObjectId.TryParse(form.AssigneeId, out var assignee) || (await employees.GetById(assignee))?.Role != Roles.ServiceDesk) ModelState.AddModelError(nameof(form.AssigneeId), "Assignee must be a service desk employee.");
        }
    }
    private async Task<TicketForm> Populate(TicketForm form)
    {
        if (!IsDesk) return form;
        var people = await employees.GetAll();
        form.Employees = people.Select(e => new SelectListItem(e.DisplayName, e.Id.ToString())).ToList();
        form.ServiceDeskEmployees = people.Where(e => e.Role == Roles.ServiceDesk).Select(e => new SelectListItem(e.DisplayName, e.Id.ToString())).ToList();
        return form;
    }
    private static void Apply(TicketForm form, Ticket ticket)
    {
        ticket.Subject = form.Subject.Trim(); ticket.Description = form.Description.Trim();
        ticket.Type = form.Type; ticket.Priority = form.Priority;
        ticket.ReportedByEmployeeId = ObjectId.Parse(form.ReporterId);
        ticket.AssignedToEmployeeId = string.IsNullOrEmpty(form.AssigneeId) ? null : ObjectId.Parse(form.AssigneeId);
        // Preserve the original time when the date itself did not change.
        if (ticket.Dates.DateReported.Date != form.EventDate!.Value.Date) ticket.Dates.DateReported = DateTime.SpecifyKind(form.EventDate.Value.Date, DateTimeKind.Utc);
        ticket.Deadline = DateTime.SpecifyKind(form.Deadline!.Value.Date, DateTimeKind.Utc);
        TicketWorkflow.SetStatus(ticket, form.Status, DateTime.UtcNow);
    }
}
