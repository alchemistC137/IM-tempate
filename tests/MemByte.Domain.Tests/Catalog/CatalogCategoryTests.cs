using MemByte.Domain.Catalog;

namespace MemByte.Domain.Tests.Catalog;

public class CatalogCategoryTests
{
	private static Guid ClientId => Guid.NewGuid();

	[Fact]
	public void Constructor_WithEmptyClientId_Throws()
	{
		Assert.Throws<ArgumentException>(() => new CatalogCategory(Guid.Empty, "Soluciones"));
	}

	[Fact]
	public void Constructor_WithEmptyName_Throws()
	{
		Assert.Throws<ArgumentException>(() => new CatalogCategory(ClientId, "   "));
	}

	[Fact]
	public void Constructor_TrimsAndAssignsName()
	{
		var category = new CatalogCategory(ClientId, "  Soluciones digitales  ");

		Assert.Equal("Soluciones digitales", category.Name);
		Assert.True(category.IsActive);
	}

	[Fact]
	public void MoveTo_WithOwnId_Throws()
	{
		var category = new CatalogCategory(ClientId, "Raíz");

		Assert.Throws<InvalidOperationException>(() => category.MoveTo(category.Id));
	}

	[Fact]
	public void MoveTo_WithDifferentParent_UpdatesParent()
	{
		var category = new CatalogCategory(ClientId, "Subcategoría");
		var parentId = Guid.NewGuid();

		category.MoveTo(parentId);

		Assert.Equal(parentId, category.ParentCategoryId);
	}

	[Fact]
	public void Deactivate_ThenActivate_TogglesIsActive()
	{
		var category = new CatalogCategory(ClientId, "Soluciones");

		category.Deactivate();
		Assert.False(category.IsActive);

		category.Activate();
		Assert.True(category.IsActive);
	}
}
