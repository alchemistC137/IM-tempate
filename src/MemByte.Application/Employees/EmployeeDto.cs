namespace MemByte.Application.Employees;

/// <summary>
/// Modelo de lectura de la ficha de un empleado, expuesto a la capa de
/// presentación. No expone comportamiento de dominio, sólo los datos
/// necesarios para listar/consultar.
/// </summary>
public sealed record EmployeeDto(
	Guid Id,
	string FirstName,
	string LastName,
	string Email,
	string Department,
	string Role,
	DateOnly HireDate,
	bool IsActive)
{
	public string FullName => $"{FirstName} {LastName}";
}
