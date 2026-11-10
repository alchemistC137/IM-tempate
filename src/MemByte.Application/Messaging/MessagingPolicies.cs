namespace MemByte.Application.Messaging;

/// <summary>
/// Nombres de las políticas de autorización del módulo de mensajería. Se
/// declaran aquí para que tanto la configuración de Identity como los
/// componentes de UI usen la misma constante.
/// </summary>
public static class MessagingPolicies
{
	/// <summary>
	/// Acceso al módulo de mensajería (leer las conversaciones propias).
	/// </summary>
	public const string AccessMessaging = "Messaging.Access";

	/// <summary>
	/// Permiso para responder conversaciones. Sólo lo tienen los empleados con
	/// rol de administración o soporte.
	/// </summary>
	public const string ReplyMessages = "Messaging.Reply";
}
