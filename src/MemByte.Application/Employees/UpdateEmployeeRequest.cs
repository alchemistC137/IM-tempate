namespace MemByte.Application.Employees;

/// <summary>
/// Datos editables de la ficha de un empleado existente.
/// </summary>
public sealed record UpdateEmployeeRequest(
	string FirstName,
	string LastName,
	string Email,
	string Department,
	string Role,
	DateOnly HireDate,
	bool IsActive);
