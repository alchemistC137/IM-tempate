namespace MemByte.Application.Notifications;

/// <summary>
/// Abstracción de envío de email, para desacoplar la capa de aplicación de
/// un proveedor concreto. La implementación de producción (SMTP/SendGrid/
/// Azure Communication Services) se decide en el runbook de despliegue;
/// mientras tanto se usa una implementación "stub" que solo registra el
/// mensaje en el log (<c>LoggingEmailSender</c> en Infrastructure).
/// </summary>
public interface IEmailSender
{
	Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
