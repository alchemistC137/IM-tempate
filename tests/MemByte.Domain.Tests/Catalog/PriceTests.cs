using MemByte.Domain.Catalog;

namespace MemByte.Domain.Tests.Catalog;

public class PriceTests
{
	[Fact]
	public void Constructor_WithValidValues_NormalizesCurrencyCode()
	{
		var price = new Price(10.5m, "eur");

		Assert.Equal(10.5m, price.Amount);
		Assert.Equal("EUR", price.CurrencyCode);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(-0.01)]
	public void Constructor_WithNegativeAmount_Throws(decimal amount)
	{
		Assert.Throws<ArgumentException>(() => new Price(amount, "EUR"));
	}

	[Theory]
	[InlineData("")]
	[InlineData("EU")]
	[InlineData("EURO")]
	[InlineData(" ")]
	public void Constructor_WithInvalidCurrencyCode_Throws(string currencyCode)
	{
		Assert.Throws<ArgumentException>(() => new Price(10, currencyCode));
	}

	[Fact]
	public void Equals_WithSameAmountAndCurrency_ReturnsTrue()
	{
		var a = new Price(10, "EUR");
		var b = new Price(10, "eur");

		Assert.Equal(a, b);
	}
}
