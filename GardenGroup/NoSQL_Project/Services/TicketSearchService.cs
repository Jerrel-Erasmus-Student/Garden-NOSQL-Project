using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using NoSQL_Project.Models;
namespace NoSQL_Project.Services;

/// <summary>Individual feature candidate: literal word search across subject and description.</summary>
public class TicketSearchService
{
    /// <summary>Builds AND/OR word matching; each word may occur in either field. Ownership is applied separately.</summary>
    public FilterDefinition<Ticket> BuildFilter(string? query, string mode)
    {
        var words = (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return Builders<Ticket>.Filter.Empty;
        var filters = words.Select(word => {
            var pattern = new BsonRegularExpression(Regex.Escape(word), "i");
            return Builders<Ticket>.Filter.Or(Builders<Ticket>.Filter.Regex(t => t.Subject, pattern), Builders<Ticket>.Filter.Regex(t => t.Description, pattern));
        });
        return mode == "OR" ? Builders<Ticket>.Filter.Or(filters) : Builders<Ticket>.Filter.And(filters);
    }
}
