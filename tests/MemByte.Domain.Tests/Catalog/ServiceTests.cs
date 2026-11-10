using MemByte.Domain.Catalog;

namespace MemByte.Domain.Tests.Catalog;

public class ServiceTests
{
	private static Guid ClientId => Guid.NewGuid();

	private static Price DefaultPrice => new(50, "EUR");

	[Fact]
	public void Constructor_WithNegativeDuration_Throws()
	{
		Assert.Throws<ArgumentException>(() => new Service(ClientId, "Consultoría", "desc", DefaultPrice, durationMinutes: -1));
	}

	[Fact]
	public void Constructor_WithNullDuration_IsAllowed()
	{
		var service = new Service(ClientId, "Consultoría", "desc", DefaultPrice);

		Assert.Null(service.DurationMinutes);
	}

	[Fact]
	public void UpdateDuration_WithNegativeValue_Throws()
	{
		var service = new Service(ClientId, "Consultoría", "desc", DefaultPrice, durationMinutes: 60);

		Assert.Throws<ArgumentException>(() => service.UpdateDuration(-5));
	}

	[Fact]
	public void SetRecurring_UpdatesFlag()
	{
		var service = new Service(ClientId, "Soporte mensual", "desc", DefaultPrice);

		service.SetRecurring(true);

		Assert.True(service.IsRecurring);
	}
}
