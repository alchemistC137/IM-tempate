using MemByte.Application.Employees;
using MemByte.Infrastructure.Employees;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Tests.Employees;

public class EmployeeServiceTests
{
	private static readonly DateOnly HireDate = new(2024, 1, 15);

	private static MemByteDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<MemByteDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
			.Options;

		return new MemByteDbContext(options);
	}

	private static CreateEmployeeRequest ValidCreateRequest(string email = "ana.garcia@membyte.dev") =>
		new("Ana", "García", email, "Desarrollo", "Employee", HireDate);

	[Fact]
	public async Task CreateAsync_WithValidData_PersistsEmployee()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		var result = await service.CreateAsync(ValidCreateRequest());

		Assert.True(result.Succeeded);
		Assert.NotNull(result.Employee);
		Assert.Equal("ana.garcia@membyte.dev", result.Employee!.Email);
		Assert.Single(dbContext.Employees);
	}

	[Fact]
	public async Task CreateAsync_WithDuplicateEmail_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		await service.CreateAsync(ValidCreateRequest());
		var result = await service.CreateAsync(ValidCreateRequest());

		Assert.False(result.Succeeded);
		Assert.Contains(result.Errors, e => e.Contains("email", StringComparison.OrdinalIgnoreCase));
		Assert.Single(dbContext.Employees);
	}

	[Fact]
	public async Task CreateAsync_WithDuplicateEmailDifferentCase_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		await service.CreateAsync(ValidCreateRequest("Ana.Garcia@membyte.dev"));
		var result = await service.CreateAsync(ValidCreateRequest("ana.garcia@MEMBYTE.dev"));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task CreateAsync_WithInvalidRole_ReturnsFailureWithoutPersisting()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		var result = await service.CreateAsync(new CreateEmployeeRequest("Ana", "García", "ana@membyte.dev", "Desarrollo", "NoExiste", HireDate));

		Assert.False(result.Succeeded);
		Assert.NotEmpty(result.Errors);
		Assert.Empty(dbContext.Employees);
	}

	[Fact]
	public async Task GetAllAsync_ReturnsEmployeesOrderedByName()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		await service.CreateAsync(new CreateEmployeeRequest("Zoe", "Alonso", "zoe@membyte.dev", "Compras", "Employee", HireDate));
		await service.CreateAsync(new CreateEmployeeRequest("Ana", "Beltrán", "ana@membyte.dev", "Compras", "Employee", HireDate));

		var employees = await service.GetAllAsync();

		Assert.Equal(2, employees.Count);
		Assert.Equal("Alonso", employees[0].LastName);
		Assert.Equal("Beltrán", employees[1].LastName);
	}

	[Fact]
	public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		var result = await service.GetByIdAsync(Guid.NewGuid());

		Assert.Null(result);
	}

	[Fact]
	public async Task UpdateAsync_WithValidData_UpdatesEmployee()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		var created = await service.CreateAsync(ValidCreateRequest());
		var id = created.Employee!.Id;

		var result = await service.UpdateAsync(id, new UpdateEmployeeRequest(
			"Ana", "García Ruiz", "ana.garcia@membyte.dev", "Compras", "PurchasingAgent", HireDate, IsActive: false));

		Assert.True(result.Succeeded);
		Assert.Equal("García Ruiz", result.Employee!.LastName);
		Assert.Equal("Compras", result.Employee.Department);
		Assert.Equal("PurchasingAgent", result.Employee.Role);
		Assert.False(result.Employee.IsActive);
	}

	[Fact]
	public async Task UpdateAsync_WithUnknownId_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateEmployeeRequest(
			"Ana", "García", "ana@membyte.dev", "Compras", "Employee", HireDate, true));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task UpdateAsync_WithEmailUsedByAnotherEmployee_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		await service.CreateAsync(ValidCreateRequest("first@membyte.dev"));
		var second = await service.CreateAsync(ValidCreateRequest("second@membyte.dev"));

		var result = await service.UpdateAsync(second.Employee!.Id, new UpdateEmployeeRequest(
			"Ana", "García", "first@membyte.dev", "Desarrollo", "Employee", HireDate, true));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task UpdateAsync_KeepingOwnEmail_Succeeds()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		var created = await service.CreateAsync(ValidCreateRequest());

		var result = await service.UpdateAsync(created.Employee!.Id, new UpdateEmployeeRequest(
			"Ana", "García", created.Employee.Email, "Desarrollo", "Employee", HireDate, true));

		Assert.True(result.Succeeded);
	}

	[Fact]
	public async Task DeleteAsync_WithExistingId_RemovesEmployee()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		var created = await service.CreateAsync(ValidCreateRequest());

		var deleted = await service.DeleteAsync(created.Employee!.Id);

		Assert.True(deleted);
		Assert.Empty(dbContext.Employees);
	}

	[Fact]
	public async Task DeleteAsync_WithUnknownId_ReturnsFalse()
	{
		await using var dbContext = CreateDbContext();
		var service = new EmployeeService(dbContext);

		var deleted = await service.DeleteAsync(Guid.NewGuid());

		Assert.False(deleted);
	}
}
