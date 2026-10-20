using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NoSQL_Project.Models;

/// <summary>Employee document; name, contact and workplace are embedded value objects.</summary>
[BsonIgnoreExtraElements]
public class Employee
{
    public ObjectId Id { get; set; }
    public Name Name { get; set; } = new();
    public Contact Contact { get; set; } = new();
    public Location Location { get; set; } = new();
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Regular;
    [BsonIgnore] public string DisplayName => $"{Name.First} {Name.Last}".Trim();
}
public class Name
{
    [BsonElement("firstName")] public string First { get; set; } = "";
    [BsonElement("lastName")] public string Last { get; set; } = "";
}
public class Contact { public string Email { get; set; } = ""; public string Phone { get; set; } = ""; }
public class Location
{
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string Street { get; set; } = "";
    public string Postcode { get; set; } = "";
}
public static class Roles
{
    public const string Regular = "Regular";
    public const string ServiceDesk = "ServiceDesk";
    public static readonly string[] All = [Regular, ServiceDesk];
}
