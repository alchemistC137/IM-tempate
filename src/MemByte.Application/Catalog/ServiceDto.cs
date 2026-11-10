namespace MemByte.Application.Catalog;

/// <summary>
/// Modelo de lectura de un servicio del catálogo.
/// </summary>
public sealed record ServiceDto(
	Guid Id,
	Guid ClientId,
	string Name,
	string Description,
	decimal PriceAmount,
	string PriceCurrencyCode,
	Guid? CategoryId,
	int? DurationMinutes,
	bool IsRecurring,
	bool IsActive,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt);
