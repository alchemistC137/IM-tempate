using MemByte.Domain.Catalog;

namespace MemByte.Domain.Tests.Catalog;

public class ProductTests
{
	private static Guid ClientId => Guid.NewGuid();

	private static Price DefaultPrice => new(100, "EUR");

	[Fact]
	public void Constructor_WithEmptyClientId_Throws()
	{
		Assert.Throws<ArgumentException>(() => new Product(Guid.Empty, "Licencia", "desc", DefaultPrice));
	}

	[Fact]
	public void Constructor_WithEmptyName_Throws()
	{
		Assert.Throws<ArgumentException>(() => new Product(ClientId, " ", "desc", DefaultPrice));
	}

	[Fact]
	public void Constructor_WithoutSku_LeavesSkuNull()
	{
		var product = new Product(ClientId, "Licencia anual", "desc", DefaultPrice);

		Assert.Null(product.Sku);
	}

	[Fact]
	public void UpdateSku_WithBlankValue_ClearsSku()
	{
		var product = new Product(ClientId, "Licencia anual", "desc", DefaultPrice, sku: "SKU-1");

		product.UpdateSku("   ");

		Assert.Null(product.Sku);
	}

	[Fact]
	public void UpdatePrice_ChangesPriceAndTouchesUpdatedAt()
	{
		var product = new Product(ClientId, "Licencia anual", "desc", DefaultPrice);
		var createdUpdatedAt = product.UpdatedAt;

		var newPrice = new Price(200, "USD");
		product.UpdatePrice(newPrice);

		Assert.Equal(newPrice, product.Price);
		Assert.True(product.UpdatedAt >= createdUpdatedAt);
	}

	[Fact]
	public void Deactivate_ThenActivate_TogglesIsActive()
	{
		var product = new Product(ClientId, "Licencia anual", "desc", DefaultPrice);

		product.Deactivate();
		Assert.False(product.IsActive);

		product.Activate();
		Assert.True(product.IsActive);
	}
}
