namespace MemByte.Application.Clients;

/// <summary>
/// Datos editables de la ficha de un cliente existente.
/// </summary>
public sealed record UpdateClientRequest(string Name, bool IsActive);
