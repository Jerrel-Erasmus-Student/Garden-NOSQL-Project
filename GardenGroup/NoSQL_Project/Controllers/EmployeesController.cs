using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using NoSQL_Project.Models;
using NoSQL_Project.Repositories.Interfaces;
using NoSQL_Project.ViewModels;

namespace NoSQL_Project.Controllers;

// Only the service desk can manage employee accounts.
[Authorize(Roles = Roles.ServiceDesk)]
public class EmployeesController : Controller
{
    private readonly IEmployeeRepository _employees;
    private readonly ITicketRepository _tickets;
    private readonly IPasswordHasher<Employee> _hasher;

    public EmployeesController(IEmployeeRepository employees, ITicketRepository tickets, IPasswordHasher<Employee> hasher)
    {
        _employees = employees;
        _tickets = tickets;
        _hasher = hasher;
    }

    public async Task<IActionResult> Index()
    {
        var allEmployees = await _employees.GetAll();
        return View(allEmployees);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View("Form", new EmployeeForm());
    }

    [HttpPost]
    public async Task<IActionResult> Create(EmployeeForm form)
    {
        form.Id = null;

        if (string.IsNullOrWhiteSpace(form.Password))
        {
            ModelState.AddModelError(nameof(form.Password), "A password of at least 12 characters is required for a new employee.");
        }

        await CheckEmailIsFree(form, currentEmployeeId: null);

        if (!ModelState.IsValid)
        {
            return View("Form", form);
        }

        var employee = new Employee { Id = ObjectId.GenerateNewId() };
        CopyFormIntoEmployee(form, employee);

        try
        {
            await _employees.Create(employee);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Two requests can race past the ModelState check above; the database's unique index catches that.
            ModelState.AddModelError(nameof(form.Email), "This email is already registered.");
            return View("Form", form);
        }

        TempData["Success"] = "Employee created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (!ObjectId.TryParse(id, out var employeeId))
        {
            return NotFound();
        }

        var employee = await _employees.GetById(employeeId);
        if (employee == null)
        {
            return NotFound();
        }

        var form = new EmployeeForm
        {
            Id = id,
            First = employee.Name.First,
            Last = employee.Name.Last,
            Email = employee.Contact.Email,
            Phone = employee.Contact.Phone,
            Role = employee.Role,
            Branch = employee.Location.Name,
            City = employee.Location.City,
            Province = employee.Location.Province,
            Street = employee.Location.Street,
            Postcode = employee.Location.Postcode
        };
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, EmployeeForm form)
    {
        if (!ObjectId.TryParse(id, out var employeeId))
        {
            return NotFound();
        }

        var employee = await _employees.GetById(employeeId);
        if (employee == null)
        {
            return NotFound();
        }

        form.Id = id;

        var editingSelf = id == User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (editingSelf && form.Role != Roles.ServiceDesk)
        {
            ModelState.AddModelError(nameof(form.Role), "You cannot remove your own service desk access.");
        }

        if (form.Role != Roles.ServiceDesk && await IsAssignedToAnyTicket(employeeId))
        {
            ModelState.AddModelError(nameof(form.Role), "Reassign this employee's tickets before changing their service desk role.");
        }

        await CheckEmailIsFree(form, employeeId);

        if (!ModelState.IsValid)
        {
            return View("Form", form);
        }

        CopyFormIntoEmployee(form, employee);

        try
        {
            var updated = await _employees.Update(employeeId, employee);
            if (!updated)
            {
                return NotFound();
            }
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            ModelState.AddModelError(nameof(form.Email), "This email is already registered.");
            return View("Form", form);
        }

        TempData["Success"] = "Employee updated. Password or role changes invalidate existing sessions.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        if (!ObjectId.TryParse(id, out var employeeId))
        {
            return NotFound();
        }

        var employee = await _employees.GetById(employeeId);
        return employee == null ? NotFound() : View(employee);
    }

    [HttpPost]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(string id, bool confirm)
    {
        if (!ObjectId.TryParse(id, out var employeeId))
        {
            return NotFound();
        }

        var employee = await _employees.GetById(employeeId);
        if (employee == null)
        {
            return NotFound();
        }

        if (!confirm)
        {
            ModelState.AddModelError("", "Confirm that you want to delete this employee.");
        }

        if (id == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            ModelState.AddModelError("", "You cannot delete your own account.");
        }

        if (await _tickets.ReferencesEmployee(employeeId))
        {
            ModelState.AddModelError("", "This employee is referenced by tickets. Reassign or remove the references before deleting the employee.");
        }

        if (!ModelState.IsValid)
        {
            return View("Delete", employee);
        }

        var deleted = await _employees.Delete(employeeId);
        if (!deleted)
        {
            return NotFound();
        }

        TempData["Success"] = "Employee deleted.";
        return RedirectToAction(nameof(Index));
    }

    // Shared helper: adds a ModelState error if the email already belongs to a different employee.
    private async Task CheckEmailIsFree(EmployeeForm form, ObjectId? currentEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(form.Email))
        {
            return;
        }

        var existing = await _employees.GetByEmail(form.Email);
        if (existing != null && existing.Id != currentEmployeeId)
        {
            ModelState.AddModelError(nameof(form.Email), "This email is already registered.");
        }
    }

    private async Task<bool> IsAssignedToAnyTicket(ObjectId employeeId)
    {
        var allTickets = await _tickets.Find(null);
        foreach (var ticket in allTickets)
        {
            if (ticket.AssignedToEmployeeId == employeeId)
            {
                return true;
            }
        }
        return false;
    }

    // Copies the submitted form values onto the Employee document that will be saved.
    private void CopyFormIntoEmployee(EmployeeForm form, Employee employee)
    {
        employee.Name = new Name { First = form.First.Trim(), Last = form.Last.Trim() };
        employee.Contact = new Contact { Email = form.Email.Trim().ToLowerInvariant(), Phone = (form.Phone ?? "").Trim() };
        employee.Location = new Location
        {
            Name = (form.Branch ?? "").Trim(),
            City = (form.City ?? "").Trim(),
            Province = (form.Province ?? "").Trim(),
            Street = (form.Street ?? "").Trim(),
            Postcode = (form.Postcode ?? "").Trim()
        };
        employee.Role = form.Role;

        if (!string.IsNullOrEmpty(form.Password))
        {
            employee.PasswordHash = _hasher.HashPassword(employee, form.Password);
        }
    }
}
