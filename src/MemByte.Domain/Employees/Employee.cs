namespace MemByte.Domain.Employees;

/// <summary>
/// Representa la ficha de un empleado (trabajador) de MemByte o de la
/// organización que use la plataforma. Es una entidad distinta e
/// independiente del <c>ApplicationUser</c> de Identity: este último es la
/// cuenta con la que un usuario accede a la intranet, mientras que
/// <see cref="Employee"/> es el registro de RRHH del trabajador (pueda o
/// no tener acceso a la plataforma).
/// </summary>
public class Employee
{
	public Guid Id { get; private set; }
	public string FirstName { get; private set; } = string.Empty;
	public string LastName { get; private set; } = string.Empty;
	public string Email { get; private set; } = string.Empty;
	public string Department { get; private set; } = string.Empty;
	public string Role { get; private set; } = EmployeeRoles.Employee;
	public DateOnly HireDate { get; private set; }
	public bool IsActive { get; private set; } = true;

	public string FullName => $"{FirstName} {LastName}";

	private Employee()
	{
		// Requerido por EF Core.
	}

	public Employee(
		string firstName,
		string lastName,
		string email,
		string department,
		string role,
		DateOnly hireDate,
		bool isActive = true)
	{
		Id = Guid.NewGuid();
		SetDetails(firstName, lastName, email, department, role, hireDate);
		IsActive = isActive;
	}

	/// <summary>
	/// Actualiza los datos editables de la ficha del empleado.
	/// </summary>
	public void UpdateDetails(
		string firstName,
		string lastName,
		string email,
		string department,
		string role,
		DateOnly hireDate)
	{
		SetDetails(firstName, lastName, email, department, role, hireDate);
	}

	public void UpdateContactInfo(string email)
	{
		if (string.IsNullOrWhiteSpace(email))
			throw new ArgumentException("El email no puede estar vacío.", nameof(email));

		Email = email;
	}

	public void Activate() => IsActive = true;

	public void Deactivate() => IsActive = false;

	private void SetDetails(
		string firstName,
		string lastName,
		string email,
		string department,
		string role,
		DateOnly hireDate)
	{
		if (string.IsNullOrWhiteSpace(firstName))
			throw new ArgumentException("El nombre no puede estar vacío.", nameof(firstName));

		if (string.IsNullOrWhiteSpace(lastName))
			throw new ArgumentException("Los apellidos no pueden estar vacíos.", nameof(lastName));

		if (string.IsNullOrWhiteSpace(email))
			throw new ArgumentException("El email no puede estar vacío.", nameof(email));

		if (string.IsNullOrWhiteSpace(department))
			throw new ArgumentException("El departamento no puede estar vacío.", nameof(department));

		if (string.IsNullOrWhiteSpace(role) || !EmployeeRoles.IsValid(role))
			throw new ArgumentException($"El rol '{role}' no es válido.", nameof(role));

		FirstName = firstName;
		LastName = lastName;
		Email = email;
		Department = department;
		Role = role;
		HireDate = hireDate;
	}
}
