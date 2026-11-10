namespace MemByte.Domain.Catalog;

/// <summary>
/// Precio de un producto o servicio del catálogo: un importe simple asociado
/// a una moneda ISO 4217. Se modela como value object inmutable para poder
/// evolucionar en el futuro hacia tarifas variables (por ejemplo, precio por
/// horas, por rango de cantidad, etc.) sin romper la forma en que se expone
/// el precio "base" de un <see cref="CatalogItem"/>.
/// </summary>
public sealed class Price : IEquatable<Price>
{
	public const int CurrencyCodeLength = 3;

	public decimal Amount { get; private set; }

	public string CurrencyCode { get; private set; } = string.Empty;

	private Price()
	{
		// Requerido por EF Core (owned type).
	}

	public Price(decimal amount, string currencyCode)
	{
		if (amount < 0)
			throw new ArgumentException("El importe no puede ser negativo.", nameof(amount));

		if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != CurrencyCodeLength)
			throw new ArgumentException("El código de moneda debe tener 3 letras (ISO 4217).", nameof(currencyCode));

		Amount = amount;
		CurrencyCode = currencyCode.Trim().ToUpperInvariant();
	}

	public bool Equals(Price? other)
		=> other is not null && Amount == other.Amount && CurrencyCode == other.CurrencyCode;

	public override bool Equals(object? obj) => Equals(obj as Price);

	public override int GetHashCode() => HashCode.Combine(Amount, CurrencyCode);

	public override string ToString() => $"{Amount} {CurrencyCode}";
}
