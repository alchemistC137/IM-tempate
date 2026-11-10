namespace MemByte.Domain.Messaging;

/// <summary>
/// Prioridad declarada de una conversación. Se usa para ordenar la bandeja
/// interna y destacar lo urgente; no otorga ningún permiso adicional.
/// </summary>
public enum ConversationPriority
{
	Low = 0,
	Normal = 1,
	High = 2,
	Urgent = 3
}
