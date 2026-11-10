using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MemByte.Web.Messaging;

/// <summary>
/// Hub de avisos de mensajería. Deliberadamente no expone métodos que el
/// cliente pueda invocar: el envío de mensajes pasa siempre por
/// <c>IMessagingService</c>, que aplica el aislamiento por cliente. El hub
/// sólo emite avisos hacia el usuario destinatario.
/// <para>
/// El destinatario se resuelve con el <c>IUserIdProvider</c> de SignalR (la
/// claim de identidad autenticada), no con un identificador enviado desde el
/// navegador: así nadie puede suscribirse a los avisos de otro usuario.
/// </para>
/// </summary>
[Authorize]
public class MessagingHub : Hub
{
	public const string HubPath = "/hubs/messaging";

	/// <summary>
	/// Nombre del método que recibe el cliente cuando hay novedades.
	/// </summary>
	public const string ConversationUpdatedMethod = "ConversationUpdated";
}
