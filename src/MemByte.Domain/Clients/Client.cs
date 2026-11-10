namespace MemByte.Domain.Clients;

/// <summary>
/// Agrupador de usuarios externos (usuarios de cliente) que acceden a la
/// aplicación frontal de MemByte, gestionados desde esta intranet. Es la
/// base mínima de la que partirá el futuro módulo de "Gestión de Proyectos y
/// Clientes" descrito en agents.md: por ahora sólo identifica y agrupa
/// cuentas (<c>ApplicationUser.ClientId</c>), sin proyectos, tarifas ni
/// facturación.
/// </summary>
public class Client
{
	public Guid Id { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public bool IsActive { get; private set; } = true;
	public DateTimeOffset CreatedAt { get; private set; }

	private Client()
	{
		// Requerido por EF Core.
	}

	public Client(string name, bool isActive = true)
	{
		Id = Guid.NewGuid();
		CreatedAt = DateTimeOffset.UtcNow;
		SetDetails(name);
		IsActive = isActive;
	}

	/// <summary>
	/// Actualiza los datos editables de la ficha del cliente.
	/// </summary>
	public void UpdateDetails(string name)
	{
		SetDetails(name);
	}

	public void Activate() => IsActive = true;

	public void Deactivate() => IsActive = false;

	private void SetDetails(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("El nombre del cliente no puede estar vacío.", nameof(name));

		Name = name;
	}
}
