using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoSQL_Project.Models;
namespace NoSQL_Project.ViewModels;

public class LoginForm
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password), StringLength(128)] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public class TicketForm
{
    public string? Id { get; set; }
    [Required, StringLength(120, MinimumLength = 3)] public string Subject { get; set; } = "";
    [Required, StringLength(5000, MinimumLength = 5)] public string Description { get; set; } = "";
    [Required, AllowedValues("Hardware", "Software", "Network", "Other")] public string Type { get; set; } = "Software";
    [Required, AllowedValues("Low", "Normal", "High", "Critical")] public string Priority { get; set; } = "Normal";
    [Required, AllowedValues("Open", "Resolved", "Closed")] public string Status { get; set; } = "Open";
    [Required, DataType(DataType.Date)] public DateTime? EventDate { get; set; } = DateTime.UtcNow.Date;
    [Required, DataType(DataType.Date)] public DateTime? Deadline { get; set; } = DateTime.UtcNow.Date.AddDays(7);
    public string? ReporterId { get; set; }
    public string? AssigneeId { get; set; }
    public List<SelectListItem> Employees { get; set; } = [];
    public List<SelectListItem> ServiceDeskEmployees { get; set; } = [];
}
public class EmployeeForm
{
    public string? Id { get; set; }
    [Required, StringLength(60)] public string First { get; set; } = "";
    [Required, StringLength(60)] public string Last { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [StringLength(30)] public string Phone { get; set; } = "";
    [Required, AllowedValues("Regular", "ServiceDesk")] public string Role { get; set; } = Roles.Regular;
    [StringLength(100)] public string Branch { get; set; } = "";
    [StringLength(100)] public string City { get; set; } = "";
    [StringLength(100)] public string Province { get; set; } = "";
    [StringLength(150)] public string Street { get; set; } = "";
    [StringLength(20)] public string Postcode { get; set; } = "";
    [DataType(DataType.Password), StringLength(128, MinimumLength = 12)] public string? Password { get; set; }
}
public class TicketList
{
    public bool Archived { get; set; }
    public List<Ticket> Tickets { get; set; } = [];
    public Dictionary<string, string> EmployeeNames { get; set; } = [];
    public string Query { get; set; } = "";
    public string Mode { get; set; } = "AND";
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / 20d));
}
public class TicketDetails
{
    public Ticket Ticket { get; set; } = new();
    public string Reporter { get; set; } = "";
    public string Assignee { get; set; } = "Unassigned";
    public List<SelectListItem> TransferRecipients { get; set; } = [];
}
public class Dashboard
{
    public int Open { get; set; }
    public int Resolved { get; set; }
    public int Closed { get; set; }
    public int Total => Open + Resolved + Closed;
    public decimal Percent(int count) => Total == 0 ? 0 : Math.Round(count * 100m / Total, 1);
    public List<Ticket> Recent { get; set; } = [];
}
