namespace MemByte.Domain.Catalog;

/// <summary>
/// Artículo de catálogo de tipo servicio (una prestación, con o sin
/// duración/recurrencia). La duración y la recurrencia son opcionales y
/// mínimas a propósito: cada cliente de la plantilla podrá extenderlas
/// según cómo entienda sus propios servicios (por horas, por proyecto,
/// suscripción, etc.).
/// </summary>
public class Service : CatalogItem
{
	public int? DurationMinutes { get; private set; }

	public bool IsRecurring { get; private set; }

	private Service()
	{
		// Requerido por EF Core.
	}

	public Service(
		Guid clientId,
		string name,
		string description,
		Price price,
		Guid? categoryId = null,
		int? durationMinutes = null,
		bool isRecurring = false,
		bool isActive = true)
		: base(clientId, name, description, price, categoryId, isActive)
	{
		SetDurationMinutes(durationMinutes);
		IsRecurring = isRecurring;
	}

	public void UpdateDuration(int? durationMinutes) => SetDurationMinutes(durationMinutes);

	public void SetRecurring(bool isRecurring) => IsRecurring = isRecurring;

	private void SetDurationMinutes(int? durationMinutes)
	{
		if (durationMinutes is < 0)
			throw new ArgumentException("La duración no puede ser negativa.", nameof(durationMinutes));

		DurationMinutes = durationMinutes;
	}
}
