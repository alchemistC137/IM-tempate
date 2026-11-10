namespace MemByte.Infrastructure.Audit;

/// <summary>
/// Publica eventos de auditoría de seguridad hacia el sistema de
/// observabilidad configurado (Application Insights / Azure Monitor vía
/// <c>ILogger</c>), en paralelo a su persistencia en <see cref="AuthAuditEntry"/>.
/// Permite definir alertas de anomalías (bloqueos, fallos repetidos, cambios
/// de MFA) sin acoplar el flujo de negocio a un proveedor concreto de APM.
/// </summary>
public interface ISecurityTelemetryPublisher
{
	void Publish(AuthAuditEntry entry);
}
