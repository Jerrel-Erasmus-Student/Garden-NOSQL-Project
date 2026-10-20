using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NoSQL_Project.Models;
namespace NoSQL_Project.Services;

/// <summary>Imports the group's MongoDB Extended JSON exports into empty collections.</summary>
public class DatabaseImporter(IMongoDatabase db, IPasswordHasher<Employee> hasher)
{
    public async Task Import(string directory, bool resetPasswords)
    {
        var employees = db.GetCollection<BsonDocument>("Employee");
        var tickets = db.GetCollection<BsonDocument>("Ticket");
        if (await employees.Find(FilterDefinition<BsonDocument>.Empty).AnyAsync() || await tickets.Find(FilterDefinition<BsonDocument>.Empty).AnyAsync())
            throw new InvalidOperationException("Import refused: use a new empty database. Existing data was not changed.");
        var people = Read(Path.Combine(directory, "Garden.Employee.json"));
        var incidents = Read(Path.Combine(directory, "Garden.Ticket.json"));
        var typedPeople = people.Select(p => BsonSerializer.Deserialize<Employee>(p)).ToDictionary(e => e.Id);
        foreach (var document in incidents) {
            var ticket = BsonSerializer.Deserialize<Ticket>(document);
            if (!typedPeople.ContainsKey(ticket.ReportedByEmployeeId) ||
                (ticket.AssignedToEmployeeId.HasValue && (!typedPeople.TryGetValue(ticket.AssignedToEmployeeId.Value, out var assignee) || assignee.Role != Roles.ServiceDesk)))
                throw new InvalidOperationException("Import contains a missing reporter or an invalid service desk assignee.");
        }
        if (resetPasswords) {
            foreach (var document in people) {
                var employee = typedPeople[document["_id"].AsObjectId];
                document["passwordHash"] = hasher.HashPassword(employee, "GardenDemo!2026");
            }
        }
        await DemoSeeder.EnsureIndexes(db);
        await employees.InsertManyAsync(people);
        await tickets.InsertManyAsync(incidents);
        Console.WriteLine($"Imported {people.Count} employees and {incidents.Count} tickets into {db.DatabaseNamespace.DatabaseName}.");
        Console.WriteLine(resetPasswords ? "Local-copy passwords: GardenDemo!2026. Original export files were not changed." : "Original password hashes retained.");
    }
    private static List<BsonDocument> Read(string path) => BsonSerializer.Deserialize<BsonArray>(File.ReadAllText(path))
        .Select(value => value.AsBsonDocument).ToList();
}
