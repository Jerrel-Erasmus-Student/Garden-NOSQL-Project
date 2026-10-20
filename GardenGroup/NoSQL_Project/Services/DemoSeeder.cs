using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using MongoDB.Driver;
using NoSQL_Project.Models;
namespace NoSQL_Project.Services;

/// <summary>Explicit, non-destructive demo setup. Never runs during ordinary startup.</summary>
public class DemoSeeder(IMongoDatabase db, IPasswordHasher<Employee> hasher)
{
    public async Task Seed()
    {
        var employees = db.GetCollection<Employee>("Employee");
        var tickets = db.GetCollection<Ticket>("Ticket");
        if (await employees.Find(_ => true).AnyAsync() || await tickets.Find(_ => true).AnyAsync())
            throw new InvalidOperationException("Demo seed refused: Employee and Ticket collections must both be empty. Choose a new database; no existing data was changed.");
        await EnsureIndexes(db);
        var people = Enumerable.Range(0, 100).Select(i => new Employee {
            Id = ObjectId.GenerateNewId(),
            Name = new Name { First = i == 0 ? "Sam" : i == 1 ? "Alex" : $"Employee{i:000}", Last = "Garden" },
            Contact = new Contact { Email = i == 0 ? "desk@gardengroup.test" : i == 1 ? "employee@gardengroup.test" : $"employee{i:000}@gardengroup.test", Phone = "0200000000" },
            Location = new Location { Name = i % 2 == 0 ? "Amsterdam" : "Haarlem", City = i % 2 == 0 ? "Amsterdam" : "Haarlem", Province = "Noord-Holland", Street = "Demo Street 1", Postcode = "1000AA" },
            Role = i == 0 || i == 2 ? Roles.ServiceDesk : Roles.Regular
        }).ToList();
        foreach (var person in people) person.PasswordHash = hasher.HashPassword(person, "GardenDemo!2026");
        await employees.InsertManyAsync(people);
        var subjects = new[] { "Laptop screen flickers", "Cannot connect to office WiFi", "Email delivery delayed", "Printer paper jam", "Inventory software login issue", "VPN connection drops", "Keyboard replacement needed", "Shared folder access request" };
        var incidents = Enumerable.Range(0, 120).Select(i => {
            var date = DateTime.UtcNow.AddDays(-(i % 45)).AddMinutes(-i);
            var ticket = new Ticket {
                Id = ObjectId.GenerateNewId(), ReportedByEmployeeId = people[i % 3 == 0 ? 1 : (i % 99) + 1].Id,
                AssignedToEmployeeId = i % 4 == 0 ? null : people[0].Id,
                Subject = subjects[i % subjects.Length], Description = $"Demo incident {i + 1}: {subjects[i % subjects.Length]}. Please investigate at the branch.",
                Type = TicketOptions.Types[i % 4], Priority = TicketOptions.Priorities[i % 4], Deadline = date.AddDays(7),
                Dates = new Dates { DateReported = date }
            };
            if (i % 4 == 1) TicketWorkflow.SetStatus(ticket, TicketOptions.Resolved, date.AddHours(1));
            if (i % 4 == 2) TicketWorkflow.SetStatus(ticket, TicketOptions.Closed, date.AddHours(2));
            if (i % 4 == 3) { TicketWorkflow.SetStatus(ticket, TicketOptions.Resolved, date.AddHours(1)); TicketWorkflow.SetStatus(ticket, TicketOptions.Closed, date.AddHours(2)); }
            return ticket;
        }).ToList();
        await tickets.InsertManyAsync(incidents);
        Console.WriteLine($"Created 100 employees and 120 tickets in {db.DatabaseNamespace.DatabaseName}.");
        Console.WriteLine("Demo accounts: desk@gardengroup.test / employee@gardengroup.test. Password: GardenDemo!2026");
    }
    public static async Task EnsureIndexes(IMongoDatabase db)
    {
        await db.GetCollection<Employee>("Employee").Indexes.CreateOneAsync(new CreateIndexModel<Employee>(
            Builders<Employee>.IndexKeys.Ascending(e => e.Contact.Email), new CreateIndexOptions { Unique = true, Name = "unique_email", Collation = new Collation("en", strength: CollationStrength.Secondary) }));
        await db.GetCollection<Ticket>("Ticket").Indexes.CreateManyAsync([
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Ascending(t => t.ReportedByEmployeeId).Descending(t => t.Dates.DateReported)),
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Ascending(t => t.AssignedToEmployeeId)),
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Descending(t => t.Dates.DateReported))
        ]);
    }
}
