namespace MemByte.Domain.Orders;

/// <summary>
/// Línea de un pedido: referencia un artículo del catálogo
/// (<see cref="CatalogItemId"/>) y conserva una copia ("snapshot") de su
/// nombre y precio en el momento en que se añadió, para que el histórico del
/// pedido no se vea afectado si el catálogo cambia después.
/// </summary>
public class OrderLine
{
	public const int ItemNameSnapshotMaxLength = 200;

	public Guid Id { get; private set; }

	public Guid OrderId { get; private set; }

	/// <summary>Artículo del catálogo (producto o servicio) ordenado.</summary>
	public Guid CatalogItemId { get; private set; }

	public string ItemNameSnapshot { get; private set; } = string.Empty;

	public decimal UnitPriceSnapshot { get; private set; }

	public string PriceCurrencyCode { get; private set; } = string.Empty;

	public int Quantity { get; private set; }

	public decimal LineTotal => UnitPriceSnapshot * Quantity;

	private OrderLine()
	{
		// Requerido por EF Core.
	}

	internal OrderLine(
		Guid orderId,
		Guid catalogItemId,
		string itemNameSnapshot,
		decimal unitPriceSnapshot,
		string priceCurrencyCode,
		int quantity)
	{
		if (orderId == Guid.Empty)
			throw new ArgumentException("La línea de pedido debe pertenecer a un pedido.", nameof(orderId));

		if (catalogItemId == Guid.Empty)
			throw new ArgumentException("La línea de pedido debe referenciar un artículo del catálogo.", nameof(catalogItemId));

		Id = Guid.NewGuid();
		OrderId = orderId;
		CatalogItemId = catalogItemId;
		SetItemNameSnapshot(itemNameSnapshot);
		SetUnitPriceSnapshot(unitPriceSnapshot);
		SetPriceCurrencyCode(priceCurrencyCode);
		SetQuantity(quantity);
	}

	internal void UpdateQuantity(int quantity) => SetQuantity(quantity);

	private void SetItemNameSnapshot(string itemNameSnapshot)
	{
		if (string.IsNullOrWhiteSpace(itemNameSnapshot))
			throw new ArgumentException("El nombre del artículo no puede estar vacío.", nameof(itemNameSnapshot));

		itemNameSnapshot = itemNameSnapshot.Trim();
		ItemNameSnapshot = itemNameSnapshot.Length > ItemNameSnapshotMaxLength
			? itemNameSnapshot[..ItemNameSnapshotMaxLength]
			: itemNameSnapshot;
	}

	private void SetUnitPriceSnapshot(decimal unitPriceSnapshot)
	{
		if (unitPriceSnapshot < 0)
			throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(unitPriceSnapshot));

		UnitPriceSnapshot = unitPriceSnapshot;
	}

	private void SetPriceCurrencyCode(string priceCurrencyCode)
	{
		if (string.IsNullOrWhiteSpace(priceCurrencyCode) || priceCurrencyCode.Trim().Length != 3)
			throw new ArgumentException("El código de moneda debe tener 3 letras (ISO 4217).", nameof(priceCurrencyCode));

		PriceCurrencyCode = priceCurrencyCode.Trim().ToUpperInvariant();
	}

	private void SetQuantity(int quantity)
	{
		if (quantity <= 0)
			throw new ArgumentException("La cantidad debe ser mayor que cero.", nameof(quantity));

		Quantity = quantity;
	}
}
