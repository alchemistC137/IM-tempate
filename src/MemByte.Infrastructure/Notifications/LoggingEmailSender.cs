using MemByte.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace MemByte.Infrastructure.Notifications;

/// <summary>
/// Implementación "stub" de <see cref="IEmailSender"/>: no envía correos
/// reales, solo registra el mensaje en el log de la aplicación con nivel
/// <see cref="LogLevel.Information"/>. Permite completar el flujo de
/// invitación de usuario y reseteo de contraseña (PR3 del plan de gestión de
/// usuarios) sin bloquear el desarrollo a la espera de decidir un proveedor
/// de email real (SMTP/SendGrid/Azure Communication Services, ver runbook de
/// despliegue). El enlace de un solo uso también se devuelve directamente al
/// Admin en la UI (<see cref="MemByte.Application.Users.UserOperationResult.ActionUrlPath"/>),
/// por lo que este stub no es la única vía para obtenerlo mientras no haya
/// envío real de correo.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
	private readonly ILogger<LoggingEmailSender> _logger;

	public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
	{
		_logger = logger;
	}

	public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
	{
		_logger.LogInformation(
			"[EMAIL STUB] Para: {To} | Asunto: {Subject}\n{Body}",
			message.To,
			message.Subject,
			message.Body);

		return Task.CompletedTask;
	}
}
