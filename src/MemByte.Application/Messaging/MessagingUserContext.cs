namespace MemByte.Application.Messaging;

/// <summary>
/// Identidad efectiva del usuario que realiza una operación de mensajería.
/// El servicio nunca acepta un identificador de usuario suelto desde la UI:
/// lo resuelve a partir de este contexto, que se construye desde las claims
/// autenticadas. Es la pieza que impide que un usuario opere en nombre de
/// otro o fuera de su cliente.
/// </summary>
/// <param name="UserId">Cuenta de Identity autenticada.</param>
/// <param name="ClientId">
/// Cliente del usuario. Nulo para el personal interno de MemByte.
/// </param>
/// <param name="IsInternalStaff">
/// Indica si el usuario es personal interno (tiene ficha de empleado). Los
/// usuarios de cliente nunca lo son.
/// </param>
/// <param name="CanReply">
/// Indica si el usuario pertenece a un rol autorizado a responder.
/// </param>
public sealed record MessagingUserContext(
	Guid UserId,
	Guid? ClientId,
	bool IsInternalStaff,
	bool CanReply);
