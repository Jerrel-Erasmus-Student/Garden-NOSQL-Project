using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using NoSQL_Project.Models;
using NoSQL_Project.Repositories.Interfaces;
namespace NoSQL_Project.Repositories;

/// <summary>MongoDB employee CRUD, adapted from the original classmate repository.</summary>
public class EmployeeRepository(IMongoDatabase db) : IEmployeeRepository
{
    private readonly IMongoCollection<Employee> employees = db.GetCollection<Employee>("Employee");
    public Task<List<Employee>> GetAll() => employees.Find(_ => true).SortBy(e => e.Name.Last).ThenBy(e => e.Name.First).ToListAsync();
    public Task<Employee?> GetById(ObjectId id) => employees.Find(e => e.Id == id).FirstOrDefaultAsync()!;
    public Task<Employee?> GetByEmail(string email) => employees.Find(Builders<Employee>.Filter.Regex(e => e.Contact.Email,
        new BsonRegularExpression("^" + Regex.Escape(email.Trim()) + "$", "i"))).FirstOrDefaultAsync()!;
    public Task Create(Employee employee) => employees.InsertOneAsync(employee);
    public async Task<bool> Update(ObjectId id, Employee employee)
    {
        employee.Id = id;
        return (await employees.ReplaceOneAsync(e => e.Id == id, employee)).MatchedCount == 1;
    }
    public async Task<bool> Delete(ObjectId id) => (await employees.DeleteOneAsync(e => e.Id == id)).DeletedCount == 1;
}
