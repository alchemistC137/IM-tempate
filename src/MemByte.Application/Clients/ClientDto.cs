namespace MemByte.Application.Clients;

/// <summary>
/// Modelo de lectura de la ficha de un cliente, expuesto a la capa de
/// presentación. No expone comportamiento de dominio, sólo los datos
/// necesarios para listar/consultar.
/// </summary>
public sealed record ClientDto(
	Guid Id,
	string Name,
	bool IsActive,
	DateTimeOffset CreatedAt);
