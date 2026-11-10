namespace MemByte.Application.Users;

/// <summary>
/// Entrada de auditoría de seguridad asociada a un usuario, expuesta de
/// solo lectura para el panel de Administración (p. ej. la pestaña
/// "Historial" de <c>/users/{id}</c>). Se apoya en el mismo registro
/// append-only que ya usa <c>IAuthService</c> (<c>AuthAuditEntry</c>), sin
/// crear un sistema de auditoría paralelo.
/// </summary>
public sealed record UserAuditEntryDto(
	Guid Id,
	string EventType,
	bool WasSuccessful,
	string? FailureReason,
	DateTimeOffset OccurredAt);
