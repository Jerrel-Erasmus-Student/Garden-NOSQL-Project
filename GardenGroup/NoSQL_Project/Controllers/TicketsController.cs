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

public class TicketsController : Controller
{
    private readonly ITicketRepository _tickets;
    private readonly IEmployeeRepository _employees;
    private readonly TicketSearchService _search;
    private readonly PrioritySortService _priorities;
    private readonly TicketTransferService _transfer;

    public TicketsController(
        ITicketRepository tickets,
        IEmployeeRepository employees,
        TicketSearchService search,
        PrioritySortService priorities,
        TicketTransferService transfer)
    {
        _tickets = tickets;
        _employees = employees;
        _search = search;
        _priorities = priorities;
        _transfer = transfer;
    }

    // Reads the signed-in employee's own ID out of their login cookie.
    private ObjectId GetCurrentEmployeeId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(rawId) || !ObjectId.TryParse(rawId, out var id))
        {
            throw new InvalidOperationException("Signed-in employee is missing a valid identifier claim.");
        }

        return id;
    }

    private bool IsServiceDesk => User.IsInRole(Roles.ServiceDesk);

    public async Task<IActionResult> Index(string? q, string mode = "AND", string sort = "newest", int page = 1, bool archived = false)
    {
        q = (q ?? "").Trim();

        var tooManyWords = q.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > 10;
        var validMode = mode == "AND" || mode == "OR";
        var validSort = sort == "newest" || sort == "priority-high" || sort == "priority-low";

        if (q.Length > 160 || tooManyWords || !validMode || !validSort)
        {
            return BadRequest("Invalid search. Use at most 10 words and 160 characters.");
        }

        // Open tickets and archived (resolved/closed) tickets are two different views over the same collection.
        FilterDefinition<Ticket> statusFilter;
        if (archived)
        {
            statusFilter = Builders<Ticket>.Filter.In(t => t.Dates.Status, new[] { TicketOptions.Resolved, TicketOptions.Closed });
        }
        else
        {
            statusFilter = Builders<Ticket>.Filter.Eq(t => t.Dates.Status, TicketOptions.Open);
        }

        ObjectId? reporterFilter = IsServiceDesk ? null : GetCurrentEmployeeId();
        var searchFilter = _search.BuildFilter(q, mode);
        var found = await _tickets.Find(reporterFilter, searchFilter & statusFilter);

        var ordered = sort == "newest" ? found : _priorities.Sort(found, sort).ToList();

        var model = new TicketList
        {
            Query = q,
            Mode = mode,
            Sort = sort,
            Total = found.Count,
            Archived = archived
        };
        model.Page = Math.Clamp(page, 1, model.Pages);
        model.Tickets = ordered.Skip((model.Page - 1) * 20).Take(20).ToList();

        if (IsServiceDesk)
        {
            var allEmployees = await _employees.GetAll();
            model.EmployeeNames = allEmployees.ToDictionary(e => e.Id.ToString(), e => e.DisplayName);
        }

        return View(model);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (!ObjectId.TryParse(id, out var ticketId))
        {
            return NotFound();
        }

        var ticket = await _tickets.GetById(ticketId);
        if (ticket == null)
        {
            return NotFound();
        }

        if (!IsServiceDesk && ticket.ReportedByEmployeeId != GetCurrentEmployeeId())
        {
            return NotFound();
        }

        var reporter = await _employees.GetById(ticket.ReportedByEmployeeId);
        Employee? assignee = null;
        if (ticket.AssignedToEmployeeId.HasValue)
        {
            assignee = await _employees.GetById(ticket.AssignedToEmployeeId.Value);
        }

        var transferRecipients = new List<SelectListItem>();
        if (IsServiceDesk)
        {
            var deskEmployees = await _employees.GetAll();
            foreach (var employee in deskEmployees)
            {
                if (employee.Role == Roles.ServiceDesk && employee.Id != ticket.AssignedToEmployeeId)
                {
                    transferRecipients.Add(new SelectListItem(employee.DisplayName, employee.Id.ToString()));
                }
            }
        }

        var model = new TicketDetails
        {
            Ticket = ticket,
            Reporter = reporter?.DisplayName ?? "Unknown employee",
            Assignee = assignee?.DisplayName ?? "Unassigned",
            TransferRecipients = transferRecipients
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var form = new TicketForm { ReporterId = GetCurrentEmployeeId().ToString() };
        return View("Form", await PopulateDropdowns(form));
    }

    [HttpPost]
    public async Task<IActionResult> Create(TicketForm form)
    {
        form.Id = null;
        // New incidents always start open; regular employees cannot choose another reporter or assignee.
        form.Status = TicketOptions.Open;
        ModelState.Remove(nameof(form.Status));

        await ValidateTicketForm(form);
        if (!ModelState.IsValid)
        {
            return View("Form", await PopulateDropdowns(form));
        }

        var ticket = new Ticket { Id = ObjectId.GenerateNewId() };
        CopyFormIntoTicket(form, ticket);
        await _tickets.Create(ticket);

        TempData["Success"] = "Ticket created. The service desk can now help you.";
        return RedirectToAction(nameof(Details), new { id = ticket.Id.ToString() });
    }

    [Authorize(Roles = Roles.ServiceDesk)]
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (!ObjectId.TryParse(id, out var ticketId))
        {
            return NotFound();
        }

        var ticket = await _tickets.GetById(ticketId);
        if (ticket == null)
        {
            return NotFound();
        }

        var form = new TicketForm
        {
            Id = id,
            Subject = ticket.Subject,
            Description = ticket.Description,
            Priority = TicketOptions.Priorities.Contains(ticket.Priority) ? ticket.Priority : "Normal",
            Type = TicketOptions.Types.Contains(ticket.Type) ? ticket.Type : "Other",
            Status = ticket.Status,
            EventDate = ticket.Dates.DateReported.Date,
            Deadline = ticket.Deadline == default ? DateTime.UtcNow.Date.AddDays(7) : ticket.Deadline.Date,
            ReporterId = ticket.ReportedByEmployeeId.ToString(),
            AssigneeId = ticket.AssignedToEmployeeId?.ToString()
        };
        return View("Form", await PopulateDropdowns(form));
    }

    [Authorize(Roles = Roles.ServiceDesk)]
    [HttpPost]
    public async Task<IActionResult> Edit(string id, TicketForm form)
    {
        if (!ObjectId.TryParse(id, out var ticketId))
        {
            return NotFound();
        }

        var ticket = await _tickets.GetById(ticketId);
        if (ticket == null)
        {
            return NotFound();
        }

        form.Id = id;
        await ValidateTicketForm(form);
        if (!ModelState.IsValid)
        {
            return View("Form", await PopulateDropdowns(form));
        }

        CopyFormIntoTicket(form, ticket);
        if (!await _tickets.Update(ticketId, ticket))
        {
            return NotFound();
        }

        TempData["Success"] = "Ticket updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.ServiceDesk)]
    [HttpPost]
    public async Task<IActionResult> Close(string id)
    {
        if (!ObjectId.TryParse(id, out var ticketId))
        {
            return NotFound();
        }

        var ticket = await _tickets.GetById(ticketId);
        if (ticket == null)
        {
            return NotFound();
        }

        TicketWorkflow.SetStatus(ticket, TicketOptions.Closed, DateTime.UtcNow);
        if (!await _tickets.Update(ticketId, ticket))
        {
            return NotFound();
        }

        TempData["Success"] = "Ticket closed. Its resolution outcome has been retained.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.ServiceDesk)]
    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        if (!ObjectId.TryParse(id, out var ticketId))
        {
            return NotFound();
        }

        var ticket = await _tickets.GetById(ticketId);
        return ticket == null ? NotFound() : View(ticket);
    }

    [Authorize(Roles = Roles.ServiceDesk)]
    [HttpPost]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(string id, bool confirm)
    {
        if (!ObjectId.TryParse(id, out var ticketId))
        {
            return NotFound();
        }

        var ticket = await _tickets.GetById(ticketId);
        if (ticket == null)
        {
            return NotFound();
        }

        if (!confirm)
        {
            ModelState.AddModelError("", "Confirm permanent deletion, or use Close to retain the incident.");
            return View("Delete", ticket);
        }

        if (!await _tickets.Delete(ticketId))
        {
            return NotFound();
        }

        TempData["Success"] = "Ticket permanently deleted.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.ServiceDesk)]
    [HttpPost]
    public async Task<IActionResult> Transfer(string id, string recipientId)
    {
        if (!ObjectId.TryParse(id, out var ticketId) || !ObjectId.TryParse(recipientId, out var recipientEmployeeId))
        {
            return BadRequest("Invalid transfer details.");
        }

        var error = await _transfer.Transfer(ticketId, recipientEmployeeId);
        if (error != null)
        {
            TempData["Error"] = error;
        }
        else
        {
            TempData["Success"] = "Ticket transferred to the selected service desk employee.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // Checks the dates and the reporter/assignee IDs on a submitted ticket form.
    private async Task ValidateTicketForm(TicketForm form)
    {
        if (form.EventDate.HasValue)
        {
            var tooLate = form.EventDate.Value.Date > DateTime.UtcNow.Date;
            var tooEarly = form.EventDate.Value.Year < 2000;
            if (tooLate || tooEarly)
            {
                ModelState.AddModelError(nameof(form.EventDate), "Choose an event date from 2000 through today (UTC).");
            }
        }

        if (form.Deadline.HasValue && form.EventDate.HasValue && form.Deadline.Value.Date < form.EventDate.Value.Date)
        {
            ModelState.AddModelError(nameof(form.Deadline), "Deadline cannot be before the event date.");
        }

        if (!IsServiceDesk)
        {
            // Regular employees can only ever report their own tickets and cannot pick an assignee.
            form.ReporterId = GetCurrentEmployeeId().ToString();
            form.AssigneeId = null;
            ModelState.Remove(nameof(form.ReporterId));
            ModelState.Remove(nameof(form.AssigneeId));
        }

        if (!ObjectId.TryParse(form.ReporterId, out var reporterId) || await _employees.GetById(reporterId) == null)
        {
            ModelState.AddModelError(nameof(form.ReporterId), "Choose an existing reporter.");
        }

        if (!string.IsNullOrEmpty(form.AssigneeId))
        {
            var validAssignee = ObjectId.TryParse(form.AssigneeId, out var assigneeId);
            var assigneeEmployee = validAssignee ? await _employees.GetById(assigneeId) : null;
            if (assigneeEmployee == null || assigneeEmployee.Role != Roles.ServiceDesk)
            {
                ModelState.AddModelError(nameof(form.AssigneeId), "Assignee must be a service desk employee.");
            }
        }
    }

    // Fills in the "reporter" and "assignee" dropdown lists, which only the service desk sees/uses.
    private async Task<TicketForm> PopulateDropdowns(TicketForm form)
    {
        if (!IsServiceDesk)
        {
            return form;
        }

        var people = await _employees.GetAll();
        form.Employees = people.Select(e => new SelectListItem(e.DisplayName, e.Id.ToString())).ToList();
        form.ServiceDeskEmployees = people.Where(e => e.Role == Roles.ServiceDesk).Select(e => new SelectListItem(e.DisplayName, e.Id.ToString())).ToList();
        return form;
    }

    // Copies the submitted form values onto the Ticket document that will be saved.
    private static void CopyFormIntoTicket(TicketForm form, Ticket ticket)
    {
        ticket.Subject = form.Subject.Trim();
        ticket.Description = form.Description.Trim();
        ticket.Type = form.Type;
        ticket.Priority = form.Priority;
        ticket.ReportedByEmployeeId = ObjectId.Parse(form.ReporterId!); // validated in ValidateTicketForm before this runs
        ticket.AssignedToEmployeeId = string.IsNullOrEmpty(form.AssigneeId) ? null : ObjectId.Parse(form.AssigneeId);

        // Preserve the original reported time when only the date (not the time) changed.
        if (form.EventDate.HasValue && ticket.Dates.DateReported.Date != form.EventDate.Value.Date)
        {
            ticket.Dates.DateReported = DateTime.SpecifyKind(form.EventDate.Value.Date, DateTimeKind.Utc);
        }

        if (form.Deadline.HasValue)
        {
            ticket.Deadline = DateTime.SpecifyKind(form.Deadline.Value.Date, DateTimeKind.Utc);
        }

        TicketWorkflow.SetStatus(ticket, form.Status, DateTime.UtcNow);
    }
}
