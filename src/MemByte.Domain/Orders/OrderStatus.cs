namespace MemByte.Domain.Orders;

/// <summary>
/// Estado del ciclo de vida de un pedido. Es un flujo mínimo pensado como
/// plantilla: cada cliente de MemByte-IM podrá ampliarlo (por ejemplo, con
/// estados intermedios de aprobación o entrega parcial) según la naturaleza
/// de lo que venda, sin cambiar el resto del módulo.
/// </summary>
public enum OrderStatus
{
	/// <summary>Pedido en preparación; admite añadir o quitar líneas.</summary>
	Draft = 0,

	/// <summary>Pedido confirmado por el cliente; ya no admite cambios de líneas.</summary>
	Confirmed = 1,

	/// <summary>Pedido en curso de entrega o prestación del servicio.</summary>
	InProgress = 2,

	/// <summary>Pedido completado; cierre normal del ciclo de vida.</summary>
	Completed = 3,

	/// <summary>Pedido cancelado; cierre anómalo del ciclo de vida.</summary>
	Cancelled = 4
}
