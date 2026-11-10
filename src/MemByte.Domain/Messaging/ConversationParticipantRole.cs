namespace MemByte.Domain.Messaging;

/// <summary>
/// Papel de un participante dentro de una conversación concreta. Es
/// independiente del rol de Identity: describe si el usuario actúa como
/// solicitante o como agente que atiende la conversación.
/// </summary>
public enum ConversationParticipantRole
{
	/// <summary>Usuario que abre/consulta (usuario de cliente o empleado).</summary>
	Requester = 0,

	/// <summary>Empleado que atiende la conversación (Admin/soporte).</summary>
	Agent = 1,

	/// <summary>Participante con acceso de sólo lectura (supervisión interna).</summary>
	Observer = 2
}
