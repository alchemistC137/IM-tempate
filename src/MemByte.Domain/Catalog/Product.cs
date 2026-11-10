namespace MemByte.Domain.Catalog;

/// <summary>
/// Artículo de catálogo de tipo producto (bien tangible o digital que se
/// entrega tal cual, sin prestación continuada). El SKU es opcional: cada
/// cliente de la plantilla puede decidir si lo usa o no según su naturaleza
/// de negocio.
/// </summary>
public class Product : CatalogItem
{
	public const int SkuMaxLength = 64;

	public string? Sku { get; private set; }

	private Product()
	{
		// Requerido por EF Core.
	}

	public Product(
		Guid clientId,
		string name,
		string description,
		Price price,
		Guid? categoryId = null,
		string? sku = null,
		bool isActive = true)
		: base(clientId, name, description, price, categoryId, isActive)
	{
		SetSku(sku);
	}

	public void UpdateSku(string? sku) => SetSku(sku);

	private void SetSku(string? sku)
	{
		if (string.IsNullOrWhiteSpace(sku))
		{
			Sku = null;
			return;
		}

		sku = sku.Trim();
		Sku = sku.Length > SkuMaxLength ? sku[..SkuMaxLength] : sku;
	}
}
