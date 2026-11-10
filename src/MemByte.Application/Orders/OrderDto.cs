using MemByte.Domain.Orders;

namespace MemByte.Application.Orders;

/// <summary>
/// Modelo de lectura completo de un pedido, incluidas sus líneas.
/// </summary>
public sealed record OrderDto(
	Guid Id,
	Guid ClientId,
	string OrderNumber,
	Guid CreatedByUserId,
	OrderStatus Status,
	OrderPaymentStatus PaymentStatus,
	string Notes,
	decimal Total,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	DateTimeOffset? ConfirmedAt,
	DateTimeOffset? CompletedAt,
	DateTimeOffset? CancelledAt,
	IReadOnlyList<OrderLineDto> Lines);

/// <summary>
/// Modelo de lectura resumido de un pedido, para listados (sin líneas).
/// </summary>
public sealed record OrderSummaryDto(
	Guid Id,
	Guid ClientId,
	string OrderNumber,
	OrderStatus Status,
	OrderPaymentStatus PaymentStatus,
	decimal Total,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt);
