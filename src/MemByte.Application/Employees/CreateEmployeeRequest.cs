namespace MemByte.Application.Employees;

/// <summary>
/// Datos necesarios para dar de alta la ficha de un nuevo empleado.
/// </summary>
public sealed record CreateEmployeeRequest(
	string FirstName,
	string LastName,
	string Email,
	string Department,
	string Role,
	DateOnly HireDate,
	bool IsActive = true);
