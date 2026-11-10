using MemByte.Application.Catalog;
using MemByte.Infrastructure.Catalog;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Tests.Catalog;

/// <summary>
/// Pruebas centradas en las garantías clave del módulo: el aislamiento entre
/// catálogos de distintos clientes y las reglas de negocio del árbol de
/// categorías (no ciclos, no borrar con dependencias).
/// </summary>
public class CatalogServiceTests
{
	private static MemByteDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<MemByteDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
			.Options;

		return new MemByteDbContext(options);
	}

	private static CatalogService CreateService(MemByteDbContext dbContext) => new(dbContext);

	private static CreateProductRequest NewProductRequest(string name = "Licencia anual", Guid? categoryId = null) =>
		new(name, "Descripción", 100m, "EUR", categoryId);

	private static CreateServiceRequest NewServiceRequest(string name = "Consultoría", Guid? categoryId = null) =>
		new(name, "Descripción", 50m, "EUR", categoryId);

	[Fact]
	public async Task GetProductsAsync_ReturnsOnlyProductsOfClient()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientA = Guid.NewGuid();
		var clientB = Guid.NewGuid();

		await service.CreateProductAsync(clientA, NewProductRequest("Producto A"));
		await service.CreateProductAsync(clientB, NewProductRequest("Producto B"));

		var productsA = await service.GetProductsAsync(clientA);

		Assert.Single(productsA);
		Assert.Equal("Producto A", productsA[0].Name);
	}

	[Fact]
	public async Task GetProductAsync_FromAnotherClient_ReturnsNull()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientA = Guid.NewGuid();
		var clientB = Guid.NewGuid();

		var created = await service.CreateProductAsync(clientA, NewProductRequest());

		var result = await service.GetProductAsync(clientB, created.Value!.Id);

		Assert.Null(result);
	}

	[Fact]
	public async Task CreateProductAsync_WithInvalidPrice_Fails()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientId = Guid.NewGuid();

		var result = await service.CreateProductAsync(clientId, new CreateProductRequest("Producto", "Desc", -1, "EUR"));

		Assert.False(result.Succeeded);
		Assert.NotEmpty(result.Errors);
	}

	[Fact]
	public async Task CreateProductAsync_WithUnknownCategory_Fails()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientId = Guid.NewGuid();

		var result = await service.CreateProductAsync(clientId, NewProductRequest(categoryId: Guid.NewGuid()));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task UpdateProductAsync_FromAnotherClient_Fails()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientA = Guid.NewGuid();
		var clientB = Guid.NewGuid();

		var created = await service.CreateProductAsync(clientA, NewProductRequest());

		var result = await service.UpdateProductAsync(
			clientB,
			created.Value!.Id,
			new UpdateProductRequest("Nuevo nombre", "Desc", 100, "EUR", null, null, true));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task SetProductActiveAsync_TogglesState()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientId = Guid.NewGuid();
		var created = await service.CreateProductAsync(clientId, NewProductRequest());

		var deactivated = await service.SetProductActiveAsync(clientId, created.Value!.Id, false);
		var product = await service.GetProductAsync(clientId, created.Value.Id);

		Assert.True(deactivated.Succeeded);
		Assert.False(product!.IsActive);
	}

	[Fact]
	public async Task DeleteProductAsync_RemovesProduct()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientId = Guid.NewGuid();
		var created = await service.CreateProductAsync(clientId, NewProductRequest());

		var deleted = await service.DeleteProductAsync(clientId, created.Value!.Id);
		var product = await service.GetProductAsync(clientId, created.Value.Id);

		Assert.True(deleted);
		Assert.Null(product);
	}

	[Fact]
	public async Task CreateServiceAsync_WithValidData_Succeeds()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientId = Guid.NewGuid();

		var result = await service.CreateServiceAsync(clientId, NewServiceRequest());

		Assert.True(result.Succeeded);
		Assert.Equal("Consultoría", result.Value!.Name);
	}

	[Fact]
	public async Task DeleteServiceAsync_FromAnotherClient_ReturnsFalse()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientA = Guid.NewGuid();
		var clientB = Guid.NewGuid();
		var created = await service.CreateServiceAsync(clientA, NewServiceRequest());

		var deleted = await service.DeleteServiceAsync(clientB, created.Value!.Id);

		Assert.False(deleted);
	}

	[Fact]
	public async Task CreateCategoryAsync_WithValidParent_Succeeds()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientId = Guid.NewGuid();
		var parent = await service.CreateCategoryAsync(clientId, new CreateCatalogCategoryRequest("Raíz"));

		var child = await service.CreateCategoryAsync(clientId, new CreateCatalogCategoryRequest("Hija", parent.Value!.Id));

		Assert.True(child.Succeeded);
		Assert.Equal(parent.Value.Id, child.Value!.ParentCategoryId);
	}

	[Fact]
	public async Task CreateCategoryAsync_WithUnknownParent_Fails()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientId = Guid.NewGuid();

		var result = await service.CreateCategoryAsync(clientId, new CreateCatalogCategoryRequest("Hija", Guid.NewGuid()));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task UpdateCategoryAsync_MovingIntoOwnDescendant_Fails()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientId = Guid.NewGuid();
		var root = await service.CreateCategoryAsync(clientId, new CreateCatalogCategoryRequest("Raíz"));
		var child = await service.CreateCategoryAsync(clientId, new CreateCatalogCategoryRequest("Hija", root.Value!.Id));

		var result = await service.UpdateCategoryAsync(
			clientId,
			root.Value.Id,
			new UpdateCatalogCategoryRequest("Raíz", child.Value!.Id, true));

		Assert.False(result.Succeeded);
		Assert.Contains(result.Errors, e => e.Contains("subárbol", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task DeleteCategoryAsync_WithChildren_Fails()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientId = Guid.NewGuid();
		var root = await service.CreateCategoryAsync(clientId, new CreateCatalogCategoryRequest("Raíz"));
		await service.CreateCategoryAsync(clientId, new CreateCatalogCategoryRequest("Hija", root.Value!.Id));

		var result = await service.DeleteCategoryAsync(clientId, root.Value.Id);

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task DeleteCategoryAsync_WithAssociatedProduct_Fails()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientId = Guid.NewGuid();
		var category = await service.CreateCategoryAsync(clientId, new CreateCatalogCategoryRequest("Soluciones"));
		await service.CreateProductAsync(clientId, NewProductRequest(categoryId: category.Value!.Id));

		var result = await service.DeleteCategoryAsync(clientId, category.Value.Id);

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task DeleteCategoryAsync_WithoutDependencies_Succeeds()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientId = Guid.NewGuid();
		var category = await service.CreateCategoryAsync(clientId, new CreateCatalogCategoryRequest("Soluciones"));

		var result = await service.DeleteCategoryAsync(clientId, category.Value!.Id);

		Assert.True(result.Succeeded);
	}

	[Fact]
	public async Task GetCategoriesAsync_IsolatesByClient()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext);
		var clientA = Guid.NewGuid();
		var clientB = Guid.NewGuid();
		await service.CreateCategoryAsync(clientA, new CreateCatalogCategoryRequest("Categoría A"));
		await service.CreateCategoryAsync(clientB, new CreateCatalogCategoryRequest("Categoría B"));

		var categoriesA = await service.GetCategoriesAsync(clientA);

		Assert.Single(categoriesA);
		Assert.Equal("Categoría A", categoriesA[0].Name);
	}
}
