namespace MemByte.Application.Clients;

/// <summary>
/// Datos necesarios para dar de alta la ficha de un nuevo cliente.
/// </summary>
public sealed record CreateClientRequest(string Name, bool IsActive = true);
