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

[Authorize(Roles = Roles.ServiceDesk)]
public class EmployeesController(IEmployeeRepository employees, ITicketRepository tickets, IPasswordHasher<Employee> hasher) : Controller
{
    public async Task<IActionResult> Index() => View(await employees.GetAll());
    [HttpGet] public IActionResult Create() => View("Form", new EmployeeForm());
    [HttpPost]
    public async Task<IActionResult> Create(EmployeeForm form)
    {
        form.Id = null;
        if (string.IsNullOrWhiteSpace(form.Password)) ModelState.AddModelError(nameof(form.Password), "A password of at least 12 characters is required for a new employee.");
        await CheckEmail(form, null);
        if (!ModelState.IsValid) return View("Form", form);
        var employee = new Employee { Id = ObjectId.GenerateNewId() };
        Apply(form, employee);
        try { await employees.Create(employee); }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) {
            ModelState.AddModelError(nameof(form.Email), "This email is already registered."); return View("Form", form);
        }
        TempData["Success"] = "Employee created.";
        return RedirectToAction(nameof(Index));
    }
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (!ObjectId.TryParse(id, out var oid)) return NotFound();
        var e = await employees.GetById(oid);
        return e == null ? NotFound() : View("Form", new EmployeeForm { Id = id, First = e.Name.First, Last = e.Name.Last, Email = e.Contact.Email, Phone = e.Contact.Phone, Role = e.Role,
            Branch = e.Location.Name, City = e.Location.City, Province = e.Location.Province, Street = e.Location.Street, Postcode = e.Location.Postcode });
    }
    [HttpPost]
    public async Task<IActionResult> Edit(string id, EmployeeForm form)
    {
        if (!ObjectId.TryParse(id, out var oid)) return NotFound();
        var employee = await employees.GetById(oid);
        if (employee == null) return NotFound();
        form.Id = id;
        if (id == User.FindFirstValue(ClaimTypes.NameIdentifier) && form.Role != Roles.ServiceDesk) ModelState.AddModelError(nameof(form.Role), "You cannot remove your own service desk access.");
        if (form.Role != Roles.ServiceDesk && await IsAssigned(oid)) ModelState.AddModelError(nameof(form.Role), "Reassign this employee's tickets before changing their service desk role.");
        await CheckEmail(form, oid);
        if (!ModelState.IsValid) return View("Form", form);
        Apply(form, employee);
        try { if (!await employees.Update(oid, employee)) return NotFound(); }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) {
            ModelState.AddModelError(nameof(form.Email), "This email is already registered."); return View("Form", form);
        }
        TempData["Success"] = "Employee updated. Password or role changes invalidate existing sessions.";
        return RedirectToAction(nameof(Index));
    }
    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        if (!ObjectId.TryParse(id, out var oid)) return NotFound();
        var employee = await employees.GetById(oid);
        return employee == null ? NotFound() : View(employee);
    }
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(string id, bool confirm)
    {
        if (!ObjectId.TryParse(id, out var oid)) return NotFound();
        var employee = await employees.GetById(oid);
        if (employee == null) return NotFound();
        if (!confirm) ModelState.AddModelError("", "Confirm that you want to delete this employee.");
        if (id == User.FindFirstValue(ClaimTypes.NameIdentifier)) ModelState.AddModelError("", "You cannot delete your own account.");
        if (await tickets.ReferencesEmployee(oid)) ModelState.AddModelError("", "This employee is referenced by tickets. Reassign or remove the references before deleting the employee.");
        if (!ModelState.IsValid) return View("Delete", employee);
        if (!await employees.Delete(oid)) return NotFound();
        TempData["Success"] = "Employee deleted.";
        return RedirectToAction(nameof(Index));
    }
    private async Task CheckEmail(EmployeeForm form, ObjectId? id)
    {
        if (string.IsNullOrWhiteSpace(form.Email)) return;
        var existing = await employees.GetByEmail(form.Email);
        if (existing != null && existing.Id != id) ModelState.AddModelError(nameof(form.Email), "This email is already registered.");
    }
    private async Task<bool> IsAssigned(ObjectId id) => (await tickets.Find(null)).Any(t => t.AssignedToEmployeeId == id);
    private void Apply(EmployeeForm form, Employee employee)
    {
        employee.Name = new Name { First = form.First.Trim(), Last = form.Last.Trim() };
        employee.Contact = new Contact { Email = form.Email.Trim().ToLowerInvariant(), Phone = (form.Phone ?? "").Trim() };
        employee.Location = new Location { Name = (form.Branch ?? "").Trim(), City = (form.City ?? "").Trim(), Province = (form.Province ?? "").Trim(), Street = (form.Street ?? "").Trim(), Postcode = (form.Postcode ?? "").Trim() };
        employee.Role = form.Role;
        if (!string.IsNullOrEmpty(form.Password)) employee.PasswordHash = hasher.HashPassword(employee, form.Password);
    }
}
