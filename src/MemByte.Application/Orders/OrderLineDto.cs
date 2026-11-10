namespace MemByte.Application.Orders;

/// <summary>
/// Modelo de lectura de una línea de pedido.
/// </summary>
public sealed record OrderLineDto(
	Guid Id,
	Guid OrderId,
	Guid CatalogItemId,
	string ItemNameSnapshot,
	decimal UnitPriceSnapshot,
	string PriceCurrencyCode,
	int Quantity,
	decimal LineTotal);
