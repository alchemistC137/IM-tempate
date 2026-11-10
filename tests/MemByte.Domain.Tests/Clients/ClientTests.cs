using MemByte.Domain.Clients;

namespace MemByte.Domain.Tests.Clients;

public class ClientTests
{
	[Fact]
	public void Constructor_WithValidData_CreatesClient()
	{
		var client = new Client("Acme Corp");

		Assert.NotEqual(Guid.Empty, client.Id);
		Assert.Equal("Acme Corp", client.Name);
		Assert.True(client.IsActive);
		Assert.NotEqual(default, client.CreatedAt);
	}

	[Fact]
	public void Constructor_WithIsActiveFalse_CreatesInactiveClient()
	{
		var client = new Client("Acme Corp", isActive: false);

		Assert.False(client.IsActive);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void Constructor_WithInvalidName_Throws(string name)
	{
		Assert.Throws<ArgumentException>(() => new Client(name));
	}

	[Fact]
	public void UpdateDetails_WithValidName_UpdatesName()
	{
		var client = new Client("Acme Corp");

		client.UpdateDetails("Acme Corporation");

		Assert.Equal("Acme Corporation", client.Name);
	}

	[Fact]
	public void UpdateDetails_WithInvalidName_ThrowsAndDoesNotChangeState()
	{
		var client = new Client("Acme Corp");

		Assert.Throws<ArgumentException>(() => client.UpdateDetails(" "));

		// El estado previo se conserva porque la validación se realiza antes de mutar.
		Assert.Equal("Acme Corp", client.Name);
	}

	[Fact]
	public void Deactivate_SetsIsActiveToFalse()
	{
		var client = new Client("Acme Corp");

		client.Deactivate();

		Assert.False(client.IsActive);
	}

	[Fact]
	public void Activate_SetsIsActiveToTrue()
	{
		var client = new Client("Acme Corp", isActive: false);

		client.Activate();

		Assert.True(client.IsActive);
	}
}
