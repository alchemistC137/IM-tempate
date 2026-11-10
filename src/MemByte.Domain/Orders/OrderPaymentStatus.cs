namespace MemByte.Domain.Orders;

/// <summary>
/// Estado de pago de un pedido. Se modela de forma mínima a propósito: cada
/// cliente de la plantilla podrá extenderlo en el futuro (pago parcial,
/// reembolsado, etc.) sin afectar al resto del módulo de pedidos.
/// </summary>
public enum OrderPaymentStatus
{
	/// <summary>Pedido pendiente de pago.</summary>
	Pending = 0,

	/// <summary>Pedido pagado en su totalidad.</summary>
	Paid = 1
}
