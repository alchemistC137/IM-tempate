namespace MemByte.Domain.Catalog;

/// <summary>
/// Categoría (o subcategoría) del catálogo de productos y servicios de un
/// cliente de la plantilla. Se organiza en árbol mediante
/// <see cref="ParentCategoryId"/>; la validación de que no se formen ciclos
/// al mover una categoría corresponde a la capa de aplicación/infraestructura,
/// que sí tiene visibilidad del árbol completo.
/// </summary>
public class CatalogCategory
{
	public const int NameMaxLength = 150;

	public Guid Id { get; private set; }

	/// <summary>
	/// Cliente propietario del catálogo. Es obligatorio: no existe un
	/// catálogo "global" sin cliente, ni siquiera para MemByte (que gestiona
	/// su propio catálogo de soluciones digitales como un cliente más).
	/// </summary>
	public Guid ClientId { get; private set; }

	public string Name { get; private set; } = string.Empty;

	public Guid? ParentCategoryId { get; private set; }

	public bool IsActive { get; private set; } = true;

	public DateTimeOffset CreatedAt { get; private set; }

	private CatalogCategory()
	{
		// Requerido por EF Core.
	}

	public CatalogCategory(Guid clientId, string name, Guid? parentCategoryId = null, bool isActive = true)
	{
		if (clientId == Guid.Empty)
			throw new ArgumentException("La categoría debe pertenecer a un cliente.", nameof(clientId));

		Id = Guid.NewGuid();
		ClientId = clientId;
		CreatedAt = DateTimeOffset.UtcNow;
		SetName(name);
		ParentCategoryId = parentCategoryId;
		IsActive = isActive;
	}

	public void Rename(string name) => SetName(name);

	/// <summary>
	/// Cambia la categoría padre. La comprobación de que el nuevo padre no
	/// pertenezca al propio subárbol (evitar ciclos) se realiza en el
	/// servicio de aplicación, que puede consultar el resto de categorías.
	/// </summary>
	public void MoveTo(Guid? parentCategoryId)
	{
		if (parentCategoryId == Id)
			throw new InvalidOperationException("Una categoría no puede ser su propio padre.");

		ParentCategoryId = parentCategoryId;
	}

	public void Activate() => IsActive = true;

	public void Deactivate() => IsActive = false;

	private void SetName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("El nombre de la categoría no puede estar vacío.", nameof(name));

		name = name.Trim();
		Name = name.Length > NameMaxLength ? name[..NameMaxLength] : name;
	}
}
