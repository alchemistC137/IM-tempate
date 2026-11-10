namespace MemByte.Application.Orders;

/// <summary>
/// Datos necesarios para crear un pedido en borrador. El número de pedido lo
/// asigna el servicio de aplicación (secuencial por cliente); el autor se
/// resuelve siempre en servidor.
/// </summary>
public sealed record CreateOrderRequest(string? Notes = null);

/// <summary>
/// Datos para añadir una línea a un pedido en borrador, referenciando un
/// artículo del catálogo (producto o servicio) por su identificador.
/// </summary>
public sealed record AddOrderLineRequest(Guid CatalogItemId, int Quantity);

/// <summary>
/// Datos para actualizar la cantidad de una línea existente.
/// </summary>
public sealed record UpdateOrderLineQuantityRequest(int Quantity);

/// <summary>
/// Datos para actualizar las notas de un pedido existente.
/// </summary>
public sealed record UpdateOrderNotesRequest(string? Notes);
