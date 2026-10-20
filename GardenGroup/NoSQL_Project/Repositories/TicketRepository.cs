using MongoDB.Bson;
using MongoDB.Driver;
using NoSQL_Project.Models;
using NoSQL_Project.Repositories.Interfaces;
namespace NoSQL_Project.Repositories;

/// <summary>MongoDB ticket CRUD and aggregation, extending the original repository.</summary>
public class TicketRepository(IMongoDatabase db) : ITicketRepository
{
    private readonly IMongoCollection<Ticket> tickets = db.GetCollection<Ticket>("Ticket");
    public Task<List<Ticket>> Find(ObjectId? reporter, FilterDefinition<Ticket>? filter = null)
    {
        var scope = reporter.HasValue ? Builders<Ticket>.Filter.Eq(t => t.ReportedByEmployeeId, reporter.Value) : Builders<Ticket>.Filter.Empty;
        return tickets.Find(scope & (filter ?? Builders<Ticket>.Filter.Empty)).SortByDescending(t => t.Dates.DateReported).ThenByDescending(t => t.Id).ToListAsync();
    }
    public Task<Ticket?> GetById(ObjectId id) => tickets.Find(t => t.Id == id).FirstOrDefaultAsync()!;
    public Task Create(Ticket ticket) => tickets.InsertOneAsync(ticket);
    public async Task<bool> Update(ObjectId id, Ticket ticket)
    {
        ticket.Id = id;
        return (await tickets.ReplaceOneAsync(t => t.Id == id, ticket)).MatchedCount == 1;
    }
    public async Task<bool> Delete(ObjectId id) => (await tickets.DeleteOneAsync(t => t.Id == id)).DeletedCount == 1;
    public async Task<bool> ReferencesEmployee(ObjectId id) => await tickets.Find(t => t.ReportedByEmployeeId == id || t.AssignedToEmployeeId == id).AnyAsync();
    public Task<List<StatusCount>> GetStatusCounts(ObjectId? reporter)
    {
        var filter = reporter.HasValue ? Builders<Ticket>.Filter.Eq(t => t.ReportedByEmployeeId, reporter.Value) : Builders<Ticket>.Filter.Empty;
        return tickets.Aggregate().Match(filter).Group(t =>
            t.Dates.Status == TicketOptions.Open ? "Open" :
            t.Dates.Status == TicketOptions.Resolved || t.Dates.SolvedAt != null ? "Resolved" : "Closed",
            g => new StatusCount { Status = g.Key, Count = g.Count() }).ToListAsync();
    }
}
