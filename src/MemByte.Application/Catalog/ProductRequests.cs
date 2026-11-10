namespace MemByte.Application.Catalog;

public sealed record CreateProductRequest(
	string Name,
	string Description,
	decimal PriceAmount,
	string PriceCurrencyCode,
	Guid? CategoryId = null,
	string? Sku = null,
	bool IsActive = true);

public sealed record UpdateProductRequest(
	string Name,
	string Description,
	decimal PriceAmount,
	string PriceCurrencyCode,
	Guid? CategoryId,
	string? Sku,
	bool IsActive);
