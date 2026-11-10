using MemByte.Application.Catalog;
using MemByte.Domain.Catalog;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Catalog;

/// <summary>
/// Implementación de <see cref="ICatalogService"/> sobre
/// <see cref="MemByteDbContext"/>. Sigue el mismo estilo que
/// <c>ClientService</c>/<c>EmployeeService</c>: sin repositorio genérico
/// adicional. Todas las consultas y mutaciones filtran siempre por el
/// <c>clientId</c> recibido, que es la barrera de aislamiento entre
/// catálogos de distintos clientes de la plantilla.
/// </summary>
public class CatalogService : ICatalogService
{
	private readonly MemByteDbContext _dbContext;

	public CatalogService(MemByteDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	// ---------------------------------------------------------------
	// Categorías.
	// ---------------------------------------------------------------

	public async Task<IReadOnlyList<CatalogCategoryDto>> GetCategoriesAsync(Guid clientId, CancellationToken cancellationToken = default)
	{
		return await _dbContext.CatalogCategories
			.AsNoTracking()
			.Where(c => c.ClientId == clientId)
			.OrderBy(c => c.Name)
			.Select(c => ToDto(c))
			.ToListAsync(cancellationToken);
	}

	public async Task<CatalogCategoryDto?> GetCategoryAsync(Guid clientId, Guid categoryId, CancellationToken cancellationToken = default)
	{
		var category = await FindCategoryAsync(clientId, categoryId, tracking: false, cancellationToken);
		return category is null ? null : ToDto(category);
	}

	public async Task<CatalogOperationResult<CatalogCategoryDto>> CreateCategoryAsync(Guid clientId, CreateCatalogCategoryRequest request, CancellationToken cancellationToken = default)
	{
		if (request.ParentCategoryId is Guid parentId)
		{
			var parentExists = await FindCategoryAsync(clientId, parentId, tracking: false, cancellationToken) is not null;
			if (!parentExists)
				return CatalogOperationResult<CatalogCategoryDto>.Failure("La categoría padre indicada no existe.");
		}

		CatalogCategory category;
		try
		{
			category = new CatalogCategory(clientId, request.Name, request.ParentCategoryId, request.IsActive);
		}
		catch (ArgumentException ex)
		{
			return CatalogOperationResult<CatalogCategoryDto>.Failure(ex.Message);
		}

		_dbContext.CatalogCategories.Add(category);
		await _dbContext.SaveChangesAsync(cancellationToken);

		return CatalogOperationResult<CatalogCategoryDto>.Success(ToDto(category));
	}

	public async Task<CatalogOperationResult<CatalogCategoryDto>> UpdateCategoryAsync(Guid clientId, Guid categoryId, UpdateCatalogCategoryRequest request, CancellationToken cancellationToken = default)
	{
		var category = await FindCategoryAsync(clientId, categoryId, tracking: true, cancellationToken);
		if (category is null)
			return CatalogOperationResult<CatalogCategoryDto>.Failure("La categoría no existe.");

		if (request.ParentCategoryId is Guid parentId)
		{
			if (parentId == categoryId)
				return CatalogOperationResult<CatalogCategoryDto>.Failure("Una categoría no puede ser su propio padre.");

			var parentExists = await FindCategoryAsync(clientId, parentId, tracking: false, cancellationToken) is not null;
			if (!parentExists)
				return CatalogOperationResult<CatalogCategoryDto>.Failure("La categoría padre indicada no existe.");

			if (await IsDescendantAsync(clientId, categoryId, parentId, cancellationToken))
				return CatalogOperationResult<CatalogCategoryDto>.Failure("No se puede mover una categoría dentro de su propio subárbol.");
		}

		try
		{
			category.Rename(request.Name);
			category.MoveTo(request.ParentCategoryId);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
		{
			return CatalogOperationResult<CatalogCategoryDto>.Failure(ex.Message);
		}

		if (request.IsActive)
			category.Activate();
		else
			category.Deactivate();

		await _dbContext.SaveChangesAsync(cancellationToken);

		return CatalogOperationResult<CatalogCategoryDto>.Success(ToDto(category));
	}

	public async Task<CatalogOperationResult<bool>> DeleteCategoryAsync(Guid clientId, Guid categoryId, CancellationToken cancellationToken = default)
	{
		var category = await FindCategoryAsync(clientId, categoryId, tracking: true, cancellationToken);
		if (category is null)
			return CatalogOperationResult<bool>.Failure("La categoría no existe.");

		var hasChildren = await _dbContext.CatalogCategories
			.AsNoTracking()
			.AnyAsync(c => c.ClientId == clientId && c.ParentCategoryId == categoryId, cancellationToken);
		if (hasChildren)
			return CatalogOperationResult<bool>.Failure("No se puede eliminar una categoría que tiene subcategorías.");

		var hasProducts = await _dbContext.Products
			.AsNoTracking()
			.AnyAsync(p => p.ClientId == clientId && p.CategoryId == categoryId, cancellationToken);
		var hasServices = await _dbContext.Services
			.AsNoTracking()
			.AnyAsync(s => s.ClientId == clientId && s.CategoryId == categoryId, cancellationToken);
		if (hasProducts || hasServices)
			return CatalogOperationResult<bool>.Failure("No se puede eliminar una categoría que tiene productos o servicios asociados.");

		_dbContext.CatalogCategories.Remove(category);
		await _dbContext.SaveChangesAsync(cancellationToken);

		return CatalogOperationResult<bool>.Success(true);
	}

	// ---------------------------------------------------------------
	// Productos.
	// ---------------------------------------------------------------

	public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(Guid clientId, CancellationToken cancellationToken = default)
	{
		return await _dbContext.Products
			.AsNoTracking()
			.Where(p => p.ClientId == clientId)
			.OrderBy(p => p.Name)
			.Select(p => ToDto(p))
			.ToListAsync(cancellationToken);
	}

	public async Task<ProductDto?> GetProductAsync(Guid clientId, Guid productId, CancellationToken cancellationToken = default)
	{
		var product = await FindProductAsync(clientId, productId, tracking: false, cancellationToken);
		return product is null ? null : ToDto(product);
	}

	public async Task<CatalogOperationResult<ProductDto>> CreateProductAsync(Guid clientId, CreateProductRequest request, CancellationToken cancellationToken = default)
	{
		if (request.CategoryId is Guid categoryId && await FindCategoryAsync(clientId, categoryId, tracking: false, cancellationToken) is null)
			return CatalogOperationResult<ProductDto>.Failure("La categoría indicada no existe.");

		Product product;
		try
		{
			var price = new Price(request.PriceAmount, request.PriceCurrencyCode);
			product = new Product(clientId, request.Name, request.Description, price, request.CategoryId, request.Sku, request.IsActive);
		}
		catch (ArgumentException ex)
		{
			return CatalogOperationResult<ProductDto>.Failure(ex.Message);
		}

		_dbContext.Products.Add(product);
		await _dbContext.SaveChangesAsync(cancellationToken);

		return CatalogOperationResult<ProductDto>.Success(ToDto(product));
	}

	public async Task<CatalogOperationResult<ProductDto>> UpdateProductAsync(Guid clientId, Guid productId, UpdateProductRequest request, CancellationToken cancellationToken = default)
	{
		var product = await FindProductAsync(clientId, productId, tracking: true, cancellationToken);
		if (product is null)
			return CatalogOperationResult<ProductDto>.Failure("El producto no existe.");

		if (request.CategoryId is Guid categoryId && await FindCategoryAsync(clientId, categoryId, tracking: false, cancellationToken) is null)
			return CatalogOperationResult<ProductDto>.Failure("La categoría indicada no existe.");

		try
		{
			var price = new Price(request.PriceAmount, request.PriceCurrencyCode);
			product.Rename(request.Name);
			product.UpdateDescription(request.Description);
			product.UpdatePrice(price);
			product.ChangeCategory(request.CategoryId);
			product.UpdateSku(request.Sku);
		}
		catch (ArgumentException ex)
		{
			return CatalogOperationResult<ProductDto>.Failure(ex.Message);
		}

		if (request.IsActive)
			product.Activate();
		else
			product.Deactivate();

		await _dbContext.SaveChangesAsync(cancellationToken);

		return CatalogOperationResult<ProductDto>.Success(ToDto(product));
	}

	public async Task<CatalogOperationResult<bool>> SetProductActiveAsync(Guid clientId, Guid productId, bool isActive, CancellationToken cancellationToken = default)
	{
		var product = await FindProductAsync(clientId, productId, tracking: true, cancellationToken);
		if (product is null)
			return CatalogOperationResult<bool>.Failure("El producto no existe.");

		if (isActive)
			product.Activate();
		else
			product.Deactivate();

		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<bool>.Success(isActive);
	}

	public async Task<bool> DeleteProductAsync(Guid clientId, Guid productId, CancellationToken cancellationToken = default)
	{
		var product = await FindProductAsync(clientId, productId, tracking: true, cancellationToken);
		if (product is null)
			return false;

		_dbContext.Products.Remove(product);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return true;
	}

	// ---------------------------------------------------------------
	// Servicios.
	// ---------------------------------------------------------------

	public async Task<IReadOnlyList<ServiceDto>> GetServicesAsync(Guid clientId, CancellationToken cancellationToken = default)
	{
		return await _dbContext.Services
			.AsNoTracking()
			.Where(s => s.ClientId == clientId)
			.OrderBy(s => s.Name)
			.Select(s => ToDto(s))
			.ToListAsync(cancellationToken);
	}

	public async Task<ServiceDto?> GetServiceAsync(Guid clientId, Guid serviceId, CancellationToken cancellationToken = default)
	{
		var service = await FindServiceAsync(clientId, serviceId, tracking: false, cancellationToken);
		return service is null ? null : ToDto(service);
	}

	public async Task<CatalogOperationResult<ServiceDto>> CreateServiceAsync(Guid clientId, CreateServiceRequest request, CancellationToken cancellationToken = default)
	{
		if (request.CategoryId is Guid categoryId && await FindCategoryAsync(clientId, categoryId, tracking: false, cancellationToken) is null)
			return CatalogOperationResult<ServiceDto>.Failure("La categoría indicada no existe.");

		Service service;
		try
		{
			var price = new Price(request.PriceAmount, request.PriceCurrencyCode);
			service = new Service(clientId, request.Name, request.Description, price, request.CategoryId, request.DurationMinutes, request.IsRecurring, request.IsActive);
		}
		catch (ArgumentException ex)
		{
			return CatalogOperationResult<ServiceDto>.Failure(ex.Message);
		}

		_dbContext.Services.Add(service);
		await _dbContext.SaveChangesAsync(cancellationToken);

		return CatalogOperationResult<ServiceDto>.Success(ToDto(service));
	}

	public async Task<CatalogOperationResult<ServiceDto>> UpdateServiceAsync(Guid clientId, Guid serviceId, UpdateServiceRequest request, CancellationToken cancellationToken = default)
	{
		var service = await FindServiceAsync(clientId, serviceId, tracking: true, cancellationToken);
		if (service is null)
			return CatalogOperationResult<ServiceDto>.Failure("El servicio no existe.");

		if (request.CategoryId is Guid categoryId && await FindCategoryAsync(clientId, categoryId, tracking: false, cancellationToken) is null)
			return CatalogOperationResult<ServiceDto>.Failure("La categoría indicada no existe.");

		try
		{
			var price = new Price(request.PriceAmount, request.PriceCurrencyCode);
			service.Rename(request.Name);
			service.UpdateDescription(request.Description);
			service.UpdatePrice(price);
			service.ChangeCategory(request.CategoryId);
			service.UpdateDuration(request.DurationMinutes);
			service.SetRecurring(request.IsRecurring);
		}
		catch (ArgumentException ex)
		{
			return CatalogOperationResult<ServiceDto>.Failure(ex.Message);
		}

		if (request.IsActive)
			service.Activate();
		else
			service.Deactivate();

		await _dbContext.SaveChangesAsync(cancellationToken);

		return CatalogOperationResult<ServiceDto>.Success(ToDto(service));
	}

	public async Task<CatalogOperationResult<bool>> SetServiceActiveAsync(Guid clientId, Guid serviceId, bool isActive, CancellationToken cancellationToken = default)
	{
		var service = await FindServiceAsync(clientId, serviceId, tracking: true, cancellationToken);
		if (service is null)
			return CatalogOperationResult<bool>.Failure("El servicio no existe.");

		if (isActive)
			service.Activate();
		else
			service.Deactivate();

		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<bool>.Success(isActive);
	}

	public async Task<bool> DeleteServiceAsync(Guid clientId, Guid serviceId, CancellationToken cancellationToken = default)
	{
		var service = await FindServiceAsync(clientId, serviceId, tracking: true, cancellationToken);
		if (service is null)
			return false;

		_dbContext.Services.Remove(service);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return true;
	}

	// ---------------------------------------------------------------
	// Auxiliares.
	// ---------------------------------------------------------------

	private Task<CatalogCategory?> FindCategoryAsync(Guid clientId, Guid categoryId, bool tracking, CancellationToken cancellationToken)
	{
		var query = tracking ? _dbContext.CatalogCategories : _dbContext.CatalogCategories.AsNoTracking();
		return query.FirstOrDefaultAsync(c => c.ClientId == clientId && c.Id == categoryId, cancellationToken);
	}

	private Task<Product?> FindProductAsync(Guid clientId, Guid productId, bool tracking, CancellationToken cancellationToken)
	{
		var query = tracking ? _dbContext.Products : _dbContext.Products.AsNoTracking();
		return query.FirstOrDefaultAsync(p => p.ClientId == clientId && p.Id == productId, cancellationToken);
	}

	private Task<Service?> FindServiceAsync(Guid clientId, Guid serviceId, bool tracking, CancellationToken cancellationToken)
	{
		var query = tracking ? _dbContext.Services : _dbContext.Services.AsNoTracking();
		return query.FirstOrDefaultAsync(s => s.ClientId == clientId && s.Id == serviceId, cancellationToken);
	}

	/// <summary>
	/// Comprueba si <paramref name="candidateParentId"/> es descendiente de
	/// <paramref name="categoryId"/> (o el mismo), para evitar ciclos al
	/// mover una categoría dentro del árbol.
	/// </summary>
	private async Task<bool> IsDescendantAsync(Guid clientId, Guid categoryId, Guid candidateParentId, CancellationToken cancellationToken)
	{
		var categories = await _dbContext.CatalogCategories
			.AsNoTracking()
			.Where(c => c.ClientId == clientId)
			.Select(c => new { c.Id, c.ParentCategoryId })
			.ToListAsync(cancellationToken);

		var currentId = (Guid?)candidateParentId;
		while (currentId is not null)
		{
			if (currentId == categoryId)
				return true;

			currentId = categories.FirstOrDefault(c => c.Id == currentId)?.ParentCategoryId;
		}

		return false;
	}

	private static CatalogCategoryDto ToDto(CatalogCategory category) => new(
		category.Id,
		category.ClientId,
		category.Name,
		category.ParentCategoryId,
		category.IsActive,
		category.CreatedAt);

	private static ProductDto ToDto(Product product) => new(
		product.Id,
		product.ClientId,
		product.Name,
		product.Description,
		product.Price.Amount,
		product.Price.CurrencyCode,
		product.CategoryId,
		product.Sku,
		product.IsActive,
		product.CreatedAt,
		product.UpdatedAt);

	private static ServiceDto ToDto(Service service) => new(
		service.Id,
		service.ClientId,
		service.Name,
		service.Description,
		service.Price.Amount,
		service.Price.CurrencyCode,
		service.CategoryId,
		service.DurationMinutes,
		service.IsRecurring,
		service.IsActive,
		service.CreatedAt,
		service.UpdatedAt);
}
