using MemByte.Application.Clients;
using MemByte.Infrastructure.Clients;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Tests.Clients;

public class ClientServiceTests
{
	private static MemByteDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<MemByteDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
			.Options;

		return new MemByteDbContext(options);
	}

	[Fact]
	public async Task CreateAsync_WithValidData_PersistsClient()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		var result = await service.CreateAsync(new CreateClientRequest("Acme Corp"));

		Assert.True(result.Succeeded);
		Assert.NotNull(result.Client);
		Assert.Equal("Acme Corp", result.Client!.Name);
		Assert.Single(dbContext.Clients);
	}

	[Fact]
	public async Task CreateAsync_WithDuplicateName_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		await service.CreateAsync(new CreateClientRequest("Acme Corp"));
		var result = await service.CreateAsync(new CreateClientRequest("Acme Corp"));

		Assert.False(result.Succeeded);
		Assert.Contains(result.Errors, e => e.Contains("nombre", StringComparison.OrdinalIgnoreCase));
		Assert.Single(dbContext.Clients);
	}

	[Fact]
	public async Task CreateAsync_WithDuplicateNameDifferentCase_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		await service.CreateAsync(new CreateClientRequest("Acme Corp"));
		var result = await service.CreateAsync(new CreateClientRequest("ACME CORP"));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task CreateAsync_WithInvalidName_ReturnsFailureWithoutPersisting()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		var result = await service.CreateAsync(new CreateClientRequest(" "));

		Assert.False(result.Succeeded);
		Assert.NotEmpty(result.Errors);
		Assert.Empty(dbContext.Clients);
	}

	[Fact]
	public async Task GetAllAsync_ReturnsClientsOrderedByName()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		await service.CreateAsync(new CreateClientRequest("Zoe Ltd"));
		await service.CreateAsync(new CreateClientRequest("Acme Corp"));

		var clients = await service.GetAllAsync();

		Assert.Equal(2, clients.Count);
		Assert.Equal("Acme Corp", clients[0].Name);
		Assert.Equal("Zoe Ltd", clients[1].Name);
	}

	[Fact]
	public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		var result = await service.GetByIdAsync(Guid.NewGuid());

		Assert.Null(result);
	}

	[Fact]
	public async Task UpdateAsync_WithValidData_UpdatesClient()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		var created = await service.CreateAsync(new CreateClientRequest("Acme Corp"));
		var id = created.Client!.Id;

		var result = await service.UpdateAsync(id, new UpdateClientRequest("Acme Corporation", false));

		Assert.True(result.Succeeded);
		Assert.Equal("Acme Corporation", result.Client!.Name);
		Assert.False(result.Client.IsActive);
	}

	[Fact]
	public async Task UpdateAsync_WithUnknownId_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateClientRequest("Acme Corp", true));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task UpdateAsync_WithNameUsedByAnotherClient_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		await service.CreateAsync(new CreateClientRequest("First Corp"));
		var second = await service.CreateAsync(new CreateClientRequest("Second Corp"));

		var result = await service.UpdateAsync(second.Client!.Id, new UpdateClientRequest("First Corp", true));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task UpdateAsync_KeepingOwnName_Succeeds()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		var created = await service.CreateAsync(new CreateClientRequest("Acme Corp"));

		var result = await service.UpdateAsync(created.Client!.Id, new UpdateClientRequest("Acme Corp", true));

		Assert.True(result.Succeeded);
	}

	[Fact]
	public async Task DeleteAsync_WithExistingId_RemovesClient()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		var created = await service.CreateAsync(new CreateClientRequest("Acme Corp"));

		var deleted = await service.DeleteAsync(created.Client!.Id);

		Assert.True(deleted);
		Assert.Empty(dbContext.Clients);
	}

	[Fact]
	public async Task DeleteAsync_WithUnknownId_ReturnsFalse()
	{
		await using var dbContext = CreateDbContext();
		var service = new ClientService(dbContext);

		var deleted = await service.DeleteAsync(Guid.NewGuid());

		Assert.False(deleted);
	}
}
