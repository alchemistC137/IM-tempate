namespace MemByte.Application.Catalog;

public sealed record CreateCatalogCategoryRequest(
	string Name,
	Guid? ParentCategoryId = null,
	bool IsActive = true);

public sealed record UpdateCatalogCategoryRequest(
	string Name,
	Guid? ParentCategoryId,
	bool IsActive);
