namespace MemByte.Domain.Messaging;

/// <summary>
/// Estado del ciclo de vida de una conversación. Permite a los agentes de
/// soporte separar lo pendiente de lo ya resuelto sin borrar el histórico.
/// </summary>
public enum ConversationStatus
{
	/// <summary>Conversación abierta, admite nuevos mensajes.</summary>
	Open = 0,

	/// <summary>Esperando respuesta del usuario que abrió la conversación.</summary>
	Pending = 1,

	/// <summary>Conversación resuelta; puede reabrirse si llega un mensaje nuevo.</summary>
	Resolved = 2,

	/// <summary>Conversación cerrada; no admite nuevos mensajes.</summary>
	Closed = 3
}
