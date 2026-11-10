namespace MemByte.Infrastructure.Messaging;

/// <summary>
/// Punto único de registro de auditoría del módulo de mensajería. Se separa
/// del servicio para poder verificar en pruebas que cada operación sensible
/// deja rastro.
/// </summary>
public interface IMessagingAuditLogger
{
	/// <summary>
	/// Añade la entrada de auditoría al contexto actual, sin persistir. Se usa
	/// cuando la operación ya va a guardar cambios.
	/// </summary>
	void Record(
		Guid userId,
		Guid? clientId,
		Guid? conversationId,
		Guid? messageId,
		string eventType,
		bool wasAllowed,
		string? details = null);

	/// <summary>
	/// Añade la entrada y la persiste inmediatamente. Se usa en los rechazos,
	/// donde no hay ninguna otra escritura que confirmar.
	/// </summary>
	Task RecordAndSaveAsync(
		Guid userId,
		Guid? clientId,
		Guid? conversationId,
		Guid? messageId,
		string eventType,
		bool wasAllowed,
		string? details = null,
		CancellationToken cancellationToken = default);
}
