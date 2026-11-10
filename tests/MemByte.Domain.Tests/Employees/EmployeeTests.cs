using MemByte.Domain.Employees;

namespace MemByte.Domain.Tests.Employees;

public class EmployeeTests
{
	private static readonly DateOnly HireDate = new(2024, 1, 15);

	[Fact]
	public void Constructor_WithValidData_CreatesEmployee()
	{
		var employee = new Employee("Ana", "García", "ana.garcia@membyte.dev", "Desarrollo", EmployeeRoles.Employee, HireDate);

		Assert.NotEqual(Guid.Empty, employee.Id);
		Assert.Equal("Ana", employee.FirstName);
		Assert.Equal("García", employee.LastName);
		Assert.Equal("Ana García", employee.FullName);
		Assert.Equal("ana.garcia@membyte.dev", employee.Email);
		Assert.Equal("Desarrollo", employee.Department);
		Assert.Equal(EmployeeRoles.Employee, employee.Role);
		Assert.Equal(HireDate, employee.HireDate);
		Assert.True(employee.IsActive);
	}

	[Fact]
	public void Constructor_WithIsActiveFalse_CreatesInactiveEmployee()
	{
		var employee = new Employee("Ana", "García", "ana.garcia@membyte.dev", "Desarrollo", EmployeeRoles.Employee, HireDate, isActive: false);

		Assert.False(employee.IsActive);
	}

	[Theory]
	[InlineData("", "García", "a@b.com", "Desarrollo", EmployeeRoles.Employee)]
	[InlineData("Ana", "", "a@b.com", "Desarrollo", EmployeeRoles.Employee)]
	[InlineData("Ana", "García", "", "Desarrollo", EmployeeRoles.Employee)]
	[InlineData("Ana", "García", "a@b.com", "", EmployeeRoles.Employee)]
	[InlineData("Ana", "García", "a@b.com", "Desarrollo", "")]
	[InlineData("Ana", "García", "a@b.com", "Desarrollo", "RolInventado")]
	public void Constructor_WithInvalidData_Throws(string firstName, string lastName, string email, string department, string role)
	{
		Assert.Throws<ArgumentException>(() =>
			new Employee(firstName, lastName, email, department, role, HireDate));
	}

	[Fact]
	public void UpdateDetails_WithValidData_UpdatesAllFields()
	{
		var employee = new Employee("Ana", "García", "ana.garcia@membyte.dev", "Desarrollo", EmployeeRoles.Employee, HireDate);
		var newHireDate = new DateOnly(2025, 3, 1);

		employee.UpdateDetails("Laura", "Gómez", "laura.gomez@membyte.dev", "Compras", EmployeeRoles.PurchasingAgent, newHireDate);

		Assert.Equal("Laura", employee.FirstName);
		Assert.Equal("Gómez", employee.LastName);
		Assert.Equal("laura.gomez@membyte.dev", employee.Email);
		Assert.Equal("Compras", employee.Department);
		Assert.Equal(EmployeeRoles.PurchasingAgent, employee.Role);
		Assert.Equal(newHireDate, employee.HireDate);
	}

	[Fact]
	public void UpdateDetails_WithInvalidRole_ThrowsAndDoesNotChangeState()
	{
		var employee = new Employee("Ana", "García", "ana.garcia@membyte.dev", "Desarrollo", EmployeeRoles.Employee, HireDate);

		Assert.Throws<ArgumentException>(() =>
			employee.UpdateDetails("Ana", "García", "ana.garcia@membyte.dev", "Desarrollo", "NoExiste", HireDate));

		// El estado previo se conserva porque la validación se realiza antes de mutar.
		Assert.Equal(EmployeeRoles.Employee, employee.Role);
	}

	[Fact]
	public void Deactivate_SetsIsActiveToFalse()
	{
		var employee = new Employee("Ana", "García", "ana.garcia@membyte.dev", "Desarrollo", EmployeeRoles.Employee, HireDate);

		employee.Deactivate();

		Assert.False(employee.IsActive);
	}

	[Fact]
	public void Activate_SetsIsActiveToTrue()
	{
		var employee = new Employee("Ana", "García", "ana.garcia@membyte.dev", "Desarrollo", EmployeeRoles.Employee, HireDate, isActive: false);

		employee.Activate();

		Assert.True(employee.IsActive);
	}

	[Fact]
	public void UpdateContactInfo_WithEmptyEmail_Throws()
	{
		var employee = new Employee("Ana", "García", "ana.garcia@membyte.dev", "Desarrollo", EmployeeRoles.Employee, HireDate);

		Assert.Throws<ArgumentException>(() => employee.UpdateContactInfo(" "));
	}

	[Fact]
	public void EmployeeRoles_All_ContainsExpectedRoles()
	{
		Assert.Equal(
			new[] { EmployeeRoles.Admin, EmployeeRoles.AreaManager, EmployeeRoles.PurchasingAgent, EmployeeRoles.Support, EmployeeRoles.Employee },
			EmployeeRoles.All);
	}
}
