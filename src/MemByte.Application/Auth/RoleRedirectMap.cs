namespace MemByte.Application.Auth;

/// <summary>
/// Mapeo de redirección post-login por rol, según la definición de
/// negocio: Admin, AreaManager y PurchasingAgent tienen dashboards propios;
/// Employee cae en el dashboard general.
/// </summary>
public static class RoleRedirectMap
{
	private static readonly IReadOnlyDictionary<string, string> Routes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["Admin"] = "/admin/dashboard",
		["AreaManager"] = "/team/dashboard",
		["PurchasingAgent"] = "/purchasing/dashboard",
		["Support"] = "/messaging",
		["Employee"] = "/dashboard"
	};

	private const string DefaultRoute = "/dashboard";

	/// <summary>
	/// Devuelve la ruta de destino para el primer rol reconocido del usuario.
	/// Si el usuario tiene varios roles, se prioriza Admin &gt; AreaManager &gt;
	/// PurchasingAgent &gt; Employee, reflejando el rol de mayor privilegio.
	/// </summary>
	public static string Resolve(IEnumerable<string> roles)
	{
		var roleSet = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);

		foreach (var role in new[] { "Admin", "AreaManager", "PurchasingAgent", "Support", "Employee" })
		{
			if (roleSet.Contains(role) && Routes.TryGetValue(role, out var route))
				return route;
		}

		return DefaultRoute;
	}
}
