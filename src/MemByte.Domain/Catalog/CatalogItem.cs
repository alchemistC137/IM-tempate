namespace MemByte.Domain.Catalog;

/// <summary>
/// Base común de todo lo que un cliente de la plantilla puede ofertar a sus
/// propios clientes: productos (<see cref="Product"/>) y servicios
/// (<see cref="Service"/>). Concentra los atributos y el comportamiento
/// compartidos (nombre, descripción, precio, categoría, estado) para que
/// cada cliente pueda especializar el catálogo según su naturaleza de
/// negocio sin duplicar estas reglas.
/// </summary>
public abstract class CatalogItem
{
	public const int NameMaxLength = 200;
	public const int DescriptionMaxLength = 2000;

	public Guid Id { get; private set; }

	/// <summary>
	/// Cliente propietario del artículo del catálogo. Obligatorio: cada
	/// catálogo pertenece a un único cliente de la plantilla.
	/// </summary>
	public Guid ClientId { get; private set; }

	public string Name { get; private set; } = string.Empty;

	public string Description { get; private set; } = string.Empty;

	public Price Price { get; private set; } = null!;

	public Guid? CategoryId { get; private set; }

	public bool IsActive { get; private set; } = true;

	public DateTimeOffset CreatedAt { get; private set; }

	public DateTimeOffset UpdatedAt { get; private set; }

	protected CatalogItem()
	{
		// Requerido por EF Core.
	}

	protected CatalogItem(
		Guid clientId,
		string name,
		string description,
		Price price,
		Guid? categoryId,
		bool isActive)
	{
		if (clientId == Guid.Empty)
			throw new ArgumentException("El artículo del catálogo debe pertenecer a un cliente.", nameof(clientId));

		Id = Guid.NewGuid();
		ClientId = clientId;
		CreatedAt = DateTimeOffset.UtcNow;
		UpdatedAt = CreatedAt;
		SetName(name);
		SetDescription(description);
		Price = price ?? throw new ArgumentNullException(nameof(price));
		CategoryId = categoryId;
		IsActive = isActive;
	}

	public void Rename(string name)
	{
		SetName(name);
		Touch();
	}

	public void UpdateDescription(string description)
	{
		SetDescription(description);
		Touch();
	}

	public void UpdatePrice(Price price)
	{
		Price = price ?? throw new ArgumentNullException(nameof(price));
		Touch();
	}

	public void ChangeCategory(Guid? categoryId)
	{
		CategoryId = categoryId;
		Touch();
	}

	public void Activate()
	{
		IsActive = true;
		Touch();
	}

	public void Deactivate()
	{
		IsActive = false;
		Touch();
	}

	private void SetName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("El nombre no puede estar vacío.", nameof(name));

		name = name.Trim();
		Name = name.Length > NameMaxLength ? name[..NameMaxLength] : name;
	}

	private void SetDescription(string description)
	{
		description = (description ?? string.Empty).Trim();
		Description = description.Length > DescriptionMaxLength ? description[..DescriptionMaxLength] : description;
	}

	private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
