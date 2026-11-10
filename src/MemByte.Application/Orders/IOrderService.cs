using MemByte.Domain.Orders;

namespace MemByte.Application.Orders;

/// <summary>
/// Servicio de aplicación para la gestión de pedidos de un cliente de la
/// plantilla. Todas las operaciones están explícitamente acotadas a un
/// <c>clientId</c>: no existe un listado global de pedidos sin cliente, cada
/// cliente gestiona los suyos (incluido MemByte, para sus propios pedidos de
/// soluciones digitales y servicios de mantenimiento).
/// </summary>
public interface IOrderService
{
	Task<IReadOnlyList<OrderSummaryDto>> GetOrdersAsync(Guid clientId, OrderStatus? status = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Lista resumida de pedidos de todos los clientes, para el panel global
	/// de gestión de pedidos.
	/// </summary>
	Task<IReadOnlyList<OrderSummaryDto>> GetAllOrdersAsync(OrderStatus? status = null, CancellationToken cancellationToken = default);

	Task<OrderDto?> GetOrderAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default);

	Task<OrderOperationResult<OrderDto>> CreateOrderAsync(Guid clientId, Guid createdByUserId, CreateOrderRequest request, CancellationToken cancellationToken = default);

	/// <summary>Actualiza las notas de un pedido; sólo se permite mientras está en borrador.</summary>
	Task<OrderOperationResult<OrderDto>> UpdateNotesAsync(Guid clientId, Guid orderId, UpdateOrderNotesRequest request, CancellationToken cancellationToken = default);

	Task<OrderOperationResult<OrderDto>> AddLineAsync(Guid clientId, Guid orderId, AddOrderLineRequest request, CancellationToken cancellationToken = default);

	Task<OrderOperationResult<OrderDto>> RemoveLineAsync(Guid clientId, Guid orderId, Guid lineId, CancellationToken cancellationToken = default);

	Task<OrderOperationResult<OrderDto>> UpdateLineQuantityAsync(Guid clientId, Guid orderId, Guid lineId, UpdateOrderLineQuantityRequest request, CancellationToken cancellationToken = default);

	Task<OrderOperationResult<OrderDto>> ConfirmOrderAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default);

	Task<OrderOperationResult<OrderDto>> StartProgressAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default);

	Task<OrderOperationResult<OrderDto>> CompleteOrderAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default);

	Task<OrderOperationResult<OrderDto>> CancelOrderAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default);

	Task<OrderOperationResult<OrderDto>> MarkAsPaidAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default);

	/// <summary>Elimina un pedido; sólo se permite mientras está en borrador.</summary>
	Task<OrderOperationResult<bool>> DeleteOrderAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default);
}
