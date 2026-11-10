using MemByte.Application.Orders;
using MemByte.Domain.Catalog;
using MemByte.Domain.Clients;
using MemByte.Domain.Orders;
using MemByte.Infrastructure.Orders;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Tests.Orders;

/// <summary>
/// Pruebas centradas en las garantías críticas del servicio: el aislamiento
/// entre clientes y las transiciones de estado válidas.
/// </summary>
public class OrderServiceTests
{
	private static MemByteDbContext CreateDbContext(string databaseName)
	{
		var options = new DbContextOptionsBuilder<MemByteDbContext>()
			.UseInMemoryDatabase(databaseName)
			.Options;

		return new MemByteDbContext(options);
	}

	private static string NewDatabaseName() => Guid.NewGuid().ToString("N");

	private static MemByteDbContext CreateDbContext() => CreateDbContext(NewDatabaseName());

	private static async Task<Product> SeedProductAsync(MemByteDbContext dbContext, Guid clientId, decimal amount = 50m)
	{
		dbContext.Clients.Add(new Client("Cliente de prueba"));
		var product = new Product(clientId, "Mantenimiento mensual", "desc", new Price(amount, "EUR"));
		dbContext.Products.Add(product);
		await dbContext.SaveChangesAsync();
		return product;
	}

	[Fact]
	public async Task CreateOrderAsync_AssignsSequentialOrderNumberPerClient()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var service = new OrderService(dbContext);

		var first = await service.CreateOrderAsync(clientId, Guid.NewGuid(), new CreateOrderRequest());
		var second = await service.CreateOrderAsync(clientId, Guid.NewGuid(), new CreateOrderRequest());

		Assert.True(first.Succeeded);
		Assert.True(second.Succeeded);
		Assert.Equal("PED-000001", first.Value!.OrderNumber);
		Assert.Equal("PED-000002", second.Value!.OrderNumber);
	}

	[Fact]
	public async Task GetOrdersAsync_ReturnsOnlyOrdersOfOwnClient()
	{
		await using var dbContext = CreateDbContext();
		var clientA = Guid.NewGuid();
		var clientB = Guid.NewGuid();
		var service = new OrderService(dbContext);

		await service.CreateOrderAsync(clientA, Guid.NewGuid(), new CreateOrderRequest());
		await service.CreateOrderAsync(clientB, Guid.NewGuid(), new CreateOrderRequest());

		var ordersOfClientA = await service.GetOrdersAsync(clientA);

		Assert.Single(ordersOfClientA);
		Assert.Equal(clientA, ordersOfClientA[0].ClientId);
	}

	[Fact]
	public async Task GetOrderAsync_FromAnotherClient_ReturnsNull()
	{
		await using var dbContext = CreateDbContext();
		var clientA = Guid.NewGuid();
		var service = new OrderService(dbContext);
		var created = await service.CreateOrderAsync(clientA, Guid.NewGuid(), new CreateOrderRequest());

		var result = await service.GetOrderAsync(Guid.NewGuid(), created.Value!.Id);

		Assert.Null(result);
	}

	[Fact]
	public async Task AddLineAsync_WithCatalogItemFromAnotherClient_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var otherClientId = Guid.NewGuid();
		var product = await SeedProductAsync(dbContext, otherClientId);

		var service = new OrderService(dbContext);
		var created = await service.CreateOrderAsync(clientId, Guid.NewGuid(), new CreateOrderRequest());

		var result = await service.AddLineAsync(clientId, created.Value!.Id, new AddOrderLineRequest(product.Id, 1));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task AddLineAsync_SnapshotsNameAndPriceFromCatalog()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var product = await SeedProductAsync(dbContext, clientId, amount: 75m);

		var service = new OrderService(dbContext);
		var created = await service.CreateOrderAsync(clientId, Guid.NewGuid(), new CreateOrderRequest());

		var result = await service.AddLineAsync(clientId, created.Value!.Id, new AddOrderLineRequest(product.Id, 2));

		Assert.True(result.Succeeded);
		var line = Assert.Single(result.Value!.Lines);
		Assert.Equal("Mantenimiento mensual", line.ItemNameSnapshot);
		Assert.Equal(75m, line.UnitPriceSnapshot);
		Assert.Equal(150m, result.Value!.Total);
	}

	[Fact]
	public async Task ConfirmOrderAsync_WithoutLines_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var service = new OrderService(dbContext);
		var created = await service.CreateOrderAsync(clientId, Guid.NewGuid(), new CreateOrderRequest());

		var result = await service.ConfirmOrderAsync(clientId, created.Value!.Id);

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task FullLifecycle_ConfirmStartCompleteAndMarkAsPaid_Succeeds()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var product = await SeedProductAsync(dbContext, clientId);
		var service = new OrderService(dbContext);

		var created = await service.CreateOrderAsync(clientId, Guid.NewGuid(), new CreateOrderRequest());
		await service.AddLineAsync(clientId, created.Value!.Id, new AddOrderLineRequest(product.Id, 1));

		var confirmed = await service.ConfirmOrderAsync(clientId, created.Value!.Id);
		Assert.True(confirmed.Succeeded);
		Assert.Equal(OrderStatus.Confirmed, confirmed.Value!.Status);

		var inProgress = await service.StartProgressAsync(clientId, created.Value!.Id);
		Assert.Equal(OrderStatus.InProgress, inProgress.Value!.Status);

		var completed = await service.CompleteOrderAsync(clientId, created.Value!.Id);
		Assert.Equal(OrderStatus.Completed, completed.Value!.Status);

		var paid = await service.MarkAsPaidAsync(clientId, created.Value!.Id);
		Assert.Equal(OrderPaymentStatus.Paid, paid.Value!.PaymentStatus);
	}

	[Fact]
	public async Task DeleteOrderAsync_WhenNotDraft_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var product = await SeedProductAsync(dbContext, clientId);
		var service = new OrderService(dbContext);

		var created = await service.CreateOrderAsync(clientId, Guid.NewGuid(), new CreateOrderRequest());
		await service.AddLineAsync(clientId, created.Value!.Id, new AddOrderLineRequest(product.Id, 1));
		await service.ConfirmOrderAsync(clientId, created.Value!.Id);

		var result = await service.DeleteOrderAsync(clientId, created.Value!.Id);

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task DeleteOrderAsync_WhenDraft_RemovesOrder()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var service = new OrderService(dbContext);
		var created = await service.CreateOrderAsync(clientId, Guid.NewGuid(), new CreateOrderRequest());

		var result = await service.DeleteOrderAsync(clientId, created.Value!.Id);

		Assert.True(result.Succeeded);
		Assert.Empty(dbContext.Orders);
	}
}
