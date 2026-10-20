using NoSQL_Project.Models;
namespace NoSQL_Project.Services;

/// <summary>Single owner of status/date invariants, shared by editing and closing.</summary>
public static class TicketWorkflow
{
    public static void SetStatus(Ticket ticket, string status, DateTime now)
    {
        if (!TicketOptions.Statuses.Contains(status)) throw new ArgumentException("Invalid ticket status.", nameof(status));
        if (status == TicketOptions.Open) { ticket.Dates.SolvedAt = null; ticket.Dates.ClosedAt = null; }
        else if (status == TicketOptions.Resolved) { ticket.Dates.SolvedAt ??= now; ticket.Dates.ClosedAt = null; }
        else { ticket.Dates.ClosedAt ??= now; }
        ticket.Status = status;
    }
}
