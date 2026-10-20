using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NoSQL_Project.Models;

/// <summary>Independent ticket document referencing the reporter and optional assignee.</summary>
[BsonIgnoreExtraElements]
public class Ticket
{
    public ObjectId Id { get; set; }
    public ObjectId ReportedByEmployeeId { get; set; }
    public ObjectId? AssignedToEmployeeId { get; set; }
    [BsonElement("title")] public string Subject { get; set; } = "";
    public string Type { get; set; } = "Not specified";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "Not specified";
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime Deadline { get; set; }
    [BsonIgnore] public string Status { get => Dates.Status; set => Dates.Status = value; }
    [BsonElement("statusInfo")] public Dates Dates { get; set; } = new();
}
public class Dates
{
    public string Status { get; set; } = TicketOptions.Open;
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime DateReported { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime? SolvedAt { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime? ClosedAt { get; set; }
}
public static class TicketOptions
{
    public const string Open = "Open", Resolved = "Resolved", Closed = "Closed";
    public static readonly string[] Statuses = [Open, Resolved, Closed];
    public static readonly string[] Priorities = ["Low", "Normal", "High", "Critical"];
    public static readonly string[] Types = ["Hardware", "Software", "Network", "Other"];
}
