using Microsoft.AspNetCore.Identity;

namespace MemByte.Infrastructure.Identity;

/// <summary>
/// Rol de aplicación (Admin, Employee, AreaManager, PurchasingAgent) tal y
/// como se definen en la matriz de permisos del proyecto.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
	public ApplicationRole()
	{
	}

	public ApplicationRole(string roleName) : base(roleName)
	{
	}
}
