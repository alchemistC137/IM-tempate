namespace MemByte.Application.Catalog;

/// <summary>
/// Modelo de lectura de una categoría del catálogo, expuesto a la capa de
/// presentación.
/// </summary>
public sealed record CatalogCategoryDto(
	Guid Id,
	Guid ClientId,
	string Name,
	Guid? ParentCategoryId,
	bool IsActive,
	DateTimeOffset CreatedAt);
