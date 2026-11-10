using Microsoft.AspNetCore.Identity;

namespace MemByte.Infrastructure.Identity;

/// <summary>
/// Usuario de autenticación de MemByte-IM. Extiende IdentityUser con los
/// campos de negocio necesarios para vincular la cuenta con un empleado
/// y con la política de MFA/roles.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
	/// <summary>
	/// Referencia al empleado (ficha de RRHH) asociado a esta cuenta, si existe.
	/// </summary>
	public Guid? EmployeeId { get; set; }

	/// <summary>
	/// Referencia al cliente (agrupador de usuarios externos) asociado a esta
	/// cuenta, si existe. Un usuario de cliente accede a la aplicación
	/// frontal de MemByte, pero se administra desde esta intranet (ver
	/// planes-de-desarrollo/03-plan-gestion-usuarios.md). Es independiente y
	/// no excluyente de <see cref="EmployeeId"/>.
	/// </summary>
	public Guid? ClientId { get; set; }

	/// <summary>
	/// Departamento/área al que pertenece el usuario, usado para scoping de
	/// permisos de AreaManager y para filtrar mensajería/compras por equipo.
	/// </summary>
	public Guid? DepartmentId { get; set; }

	/// <summary>
	/// Zona horaria preferida del usuario, usada para mostrar fechas/horas.
	/// </summary>
	public string Timezone { get; set; } = "UTC";

	/// <summary>
	/// Marca de tiempo del último inicio de sesión correcto.
	/// </summary>
	public DateTimeOffset? LastLoginAt { get; set; }

	/// <summary>
	/// Indica si el usuario está obligado a activar MFA (política configurable
	/// por rol/tenant). Por defecto, MFA es opcional en el lanzamiento inicial.
	/// </summary>
	public bool IsMfaRequired { get; set; }

	/// <summary>
	/// Nombre completo para mostrar en la UI (saludo post-login, auditoría, etc.).
	/// </summary>
	public string DisplayName { get; set; } = string.Empty;
}
