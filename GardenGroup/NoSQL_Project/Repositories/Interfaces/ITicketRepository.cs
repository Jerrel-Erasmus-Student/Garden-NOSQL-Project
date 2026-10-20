using MongoDB.Bson;
using MongoDB.Driver;
using NoSQL_Project.Models;
namespace NoSQL_Project.Repositories.Interfaces;

public class StatusCount { public string Status { get; set; } = ""; public int Count { get; set; } }
/// <summary>Ticket persistence. A null reporter scope is reserved for service desk callers.</summary>
public interface ITicketRepository
{
    /// <summary>Returns scoped matches, newest first. Extra filters are ANDed with ownership.</summary>
    Task<List<Ticket>> Find(ObjectId? reporter, FilterDefinition<Ticket>? filter = null);
    /// <summary>Gets a ticket by ID, or null; the caller must enforce ownership.</summary>
    Task<Ticket?> GetById(ObjectId id);
    /// <summary>Inserts a validated ticket.</summary>
    Task Create(Ticket ticket);
    /// <summary>Replaces a ticket under its existing ID; false when missing.</summary>
    Task<bool> Update(ObjectId id, Ticket ticket);
    /// <summary>Permanently deletes a ticket. Only the service desk may invoke this operation.</summary>
    Task<bool> Delete(ObjectId id);
    /// <summary>Aggregates mutually exclusive open/resolved/closed-unsolved counts in MongoDB.</summary>
    Task<List<StatusCount>> GetStatusCounts(ObjectId? reporter);
    /// <summary>Checks both reporter and assignee references before employee deletion.</summary>
    Task<bool> ReferencesEmployee(ObjectId id);
}
