using MemByte.Application.Employees;
using MemByte.Domain.Employees;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Employees;

/// <summary>
/// Implementación de <see cref="IEmployeeService"/> sobre <see cref="MemByteDbContext"/>.
/// Sigue el mismo estilo que <c>AuthService</c>: sin repositorio genérico
/// adicional, el DbContext se usa directamente desde el servicio de
/// aplicación/infraestructura.
/// </summary>
public class EmployeeService : IEmployeeService
{
	private readonly MemByteDbContext _dbContext;

	public EmployeeService(MemByteDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return await _dbContext.Employees
			.AsNoTracking()
			.OrderBy(e => e.LastName)
			.ThenBy(e => e.FirstName)
			.Select(e => ToDto(e))
			.ToListAsync(cancellationToken);
	}

	public async Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var employee = await _dbContext.Employees
			.AsNoTracking()
			.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

		return employee is null ? null : ToDto(employee);
	}

	public async Task<EmployeeOperationResult> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default)
	{
		if (await EmailInUseAsync(request.Email, excludeId: null, cancellationToken))
			return EmployeeOperationResult.Failure("Ya existe un empleado con ese email.");

		Employee employee;
		try
		{
			employee = new Employee(
				request.FirstName,
				request.LastName,
				request.Email,
				request.Department,
				request.Role,
				request.HireDate,
				request.IsActive);
		}
		catch (ArgumentException ex)
		{
			return EmployeeOperationResult.Failure(ex.Message);
		}

		_dbContext.Employees.Add(employee);
		await _dbContext.SaveChangesAsync(cancellationToken);

		return EmployeeOperationResult.Success(ToDto(employee));
	}

	public async Task<EmployeeOperationResult> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken = default)
	{
		var employee = await _dbContext.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
		if (employee is null)
			return EmployeeOperationResult.Failure("El empleado no existe.");

		if (await EmailInUseAsync(request.Email, excludeId: id, cancellationToken))
			return EmployeeOperationResult.Failure("Ya existe un empleado con ese email.");

		try
		{
			employee.UpdateDetails(
				request.FirstName,
				request.LastName,
				request.Email,
				request.Department,
				request.Role,
				request.HireDate);
		}
		catch (ArgumentException ex)
		{
			return EmployeeOperationResult.Failure(ex.Message);
		}

		if (request.IsActive)
			employee.Activate();
		else
			employee.Deactivate();

		await _dbContext.SaveChangesAsync(cancellationToken);

		return EmployeeOperationResult.Success(ToDto(employee));
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var employee = await _dbContext.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
		if (employee is null)
			return false;

		_dbContext.Employees.Remove(employee);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return true;
	}

	private Task<bool> EmailInUseAsync(string email, Guid? excludeId, CancellationToken cancellationToken)
	{
		return _dbContext.Employees
			.AsNoTracking()
			.AnyAsync(e => e.Email.ToLower() == email.ToLower() && (excludeId == null || e.Id != excludeId), cancellationToken);
	}

	private static EmployeeDto ToDto(Employee employee) => new(
		employee.Id,
		employee.FirstName,
		employee.LastName,
		employee.Email,
		employee.Department,
		employee.Role,
		employee.HireDate,
		employee.IsActive);
}
