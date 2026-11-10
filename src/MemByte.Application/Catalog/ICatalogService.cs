namespace MemByte.Application.Catalog;

/// <summary>
/// Servicio de aplicación para la gestión del catálogo de productos y
/// servicios de un cliente de la plantilla (categorías, productos y
/// servicios). Todas las operaciones están explícitamente acotadas a un
/// <c>clientId</c>: no existe un catálogo global, cada cliente gestiona el
/// suyo (incluido MemByte, para su propio catálogo de soluciones
/// digitales).
/// </summary>
public interface ICatalogService
{
	// Categorías.
	Task<IReadOnlyList<CatalogCategoryDto>> GetCategoriesAsync(Guid clientId, CancellationToken cancellationToken = default);

	Task<CatalogCategoryDto?> GetCategoryAsync(Guid clientId, Guid categoryId, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<CatalogCategoryDto>> CreateCategoryAsync(Guid clientId, CreateCatalogCategoryRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<CatalogCategoryDto>> UpdateCategoryAsync(Guid clientId, Guid categoryId, UpdateCatalogCategoryRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<bool>> DeleteCategoryAsync(Guid clientId, Guid categoryId, CancellationToken cancellationToken = default);

	// Productos.
	Task<IReadOnlyList<ProductDto>> GetProductsAsync(Guid clientId, CancellationToken cancellationToken = default);

	Task<ProductDto?> GetProductAsync(Guid clientId, Guid productId, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<ProductDto>> CreateProductAsync(Guid clientId, CreateProductRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<ProductDto>> UpdateProductAsync(Guid clientId, Guid productId, UpdateProductRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<bool>> SetProductActiveAsync(Guid clientId, Guid productId, bool isActive, CancellationToken cancellationToken = default);

	Task<bool> DeleteProductAsync(Guid clientId, Guid productId, CancellationToken cancellationToken = default);

	// Servicios.
	Task<IReadOnlyList<ServiceDto>> GetServicesAsync(Guid clientId, CancellationToken cancellationToken = default);

	Task<ServiceDto?> GetServiceAsync(Guid clientId, Guid serviceId, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<ServiceDto>> CreateServiceAsync(Guid clientId, CreateServiceRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<ServiceDto>> UpdateServiceAsync(Guid clientId, Guid serviceId, UpdateServiceRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<bool>> SetServiceActiveAsync(Guid clientId, Guid serviceId, bool isActive, CancellationToken cancellationToken = default);

	Task<bool> DeleteServiceAsync(Guid clientId, Guid serviceId, CancellationToken cancellationToken = default);
}
