using MongoDB.Bson;
using NoSQL_Project.Models;
using NoSQL_Project.Repositories.Interfaces;
namespace NoSQL_Project.Services;

/// <summary>Individual feature candidate: explicit transfer with a validated service desk recipient.</summary>
public class TicketTransferService(ITicketRepository tickets, IEmployeeRepository employees)
{
    /// <summary>Transfers an existing ticket to another service desk employee, leaving status/dates unchanged.</summary>
    public async Task<string?> Transfer(ObjectId ticketId, ObjectId recipientId)
    {
        var ticket = await tickets.GetById(ticketId);
        if (ticket == null) return "Ticket no longer exists.";
        var recipient = await employees.GetById(recipientId);
        if (recipient?.Role != Roles.ServiceDesk) return "Choose an existing service desk employee.";
        if (ticket.AssignedToEmployeeId == recipientId) return "This ticket is already assigned to that employee.";
        ticket.AssignedToEmployeeId = recipientId;
        return await tickets.Update(ticketId, ticket) ? null : "Ticket no longer exists.";
    }
}
