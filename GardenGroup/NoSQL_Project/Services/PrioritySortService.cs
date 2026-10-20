using NoSQL_Project.Models;
namespace NoSQL_Project.Services;

/// <summary>Separate individual feature candidate: semantic priority ordering, not alphabetic ordering.</summary>
public class PrioritySortService
{
    public static int Rank(string priority) => priority switch { "Critical" => 4, "High" => 3, "Normal" => 2, "Low" => 1, _ => 0 };
    /// <summary>Orders priority high/low, using newest reported date and ID as deterministic tie breakers.</summary>
    public IEnumerable<Ticket> Sort(IEnumerable<Ticket> tickets, string direction) => direction == "priority-low"
        ? tickets.OrderBy(t => Rank(t.Priority)).ThenByDescending(t => t.Dates.DateReported).ThenByDescending(t => t.Id)
        : tickets.OrderByDescending(t => Rank(t.Priority)).ThenByDescending(t => t.Dates.DateReported).ThenByDescending(t => t.Id);
}
