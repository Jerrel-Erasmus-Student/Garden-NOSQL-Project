using MongoDB.Bson;
using NoSQL_Project.Models;
namespace NoSQL_Project.Repositories.Interfaces;

/// <summary>Employee persistence boundary. Missing lookups return null; writes return success.</summary>
public interface IEmployeeRepository
{
    /// <summary>Returns all employees ordered by surname.</summary>
    Task<List<Employee>> GetAll();
    /// <summary>Finds one employee by MongoDB identifier, or null.</summary>
    Task<Employee?> GetById(ObjectId id);
    /// <summary>Finds a case-insensitive exact email match, or null.</summary>
    Task<Employee?> GetByEmail(string email);
    /// <summary>Inserts an employee; duplicate emails are rejected by the unique index.</summary>
    Task Create(Employee employee);
    /// <summary>Replaces an existing employee, preserving its identifier.</summary>
    Task<bool> Update(ObjectId id, Employee employee);
    /// <summary>Deletes an employee; the service layer must first check ticket references.</summary>
    Task<bool> Delete(ObjectId id);
}
