using Microsoft.Extensions.Logging;

namespace MemByte.Infrastructure.Audit;

/// <summary>
/// Implementación por defecto de <see cref="ISecurityTelemetryPublisher"/>:
/// emite cada evento de auditoría como un log estructurado con
/// <c>EventType</c>/<c>UserId</c>/<c>WasSuccessful</c> como propiedades
/// (custom dimensions). Cuando Application Insights está configurado
/// (<c>AddApplicationInsightsTelemetry</c> en <c>Program.cs</c>), el proveedor
/// de logging de AI reenvía automáticamente estas entradas como <c>traces</c>,
/// lo que permite construir alertas KQL sobre ellas sin código adicional
/// (ver runbook de despliegue, sección "Telemetría y alertas").
/// </summary>
public class SecurityTelemetryPublisher : ISecurityTelemetryPublisher
{
	/// <summary>
	/// Tipos de evento considerados señal de posible abuso o incidente de
	/// seguridad; se registran en <see cref="LogLevel.Warning"/> para que las
	/// reglas de alerta basadas en severidad (Azure Monitor) los detecten sin
	/// necesidad de filtrar por texto.
	/// </summary>
	private static readonly HashSet<string> AnomalousEventTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"LoginFailed",
		"AccountLockedOut",
		"LoginNotAllowed",
		"MfaInvalidCode",
		"InvalidRecoveryCode",
	};

	private readonly ILogger<SecurityTelemetryPublisher> _logger;

	public SecurityTelemetryPublisher(ILogger<SecurityTelemetryPublisher> logger)
	{
		_logger = logger;
	}

	public void Publish(AuthAuditEntry entry)
	{
		var level = AnomalousEventTypes.Contains(entry.EventType) ? LogLevel.Warning : LogLevel.Information;

		_logger.Log(
			level,
			new EventId(0, entry.EventType),
			"SecurityAudit {EventType} UserId={UserId} Success={WasSuccessful} Reason={FailureReason} Ip={IpAddress}",
			entry.EventType,
			entry.UserId,
			entry.WasSuccessful,
			entry.FailureReason,
			entry.IpAddress);
	}
}
