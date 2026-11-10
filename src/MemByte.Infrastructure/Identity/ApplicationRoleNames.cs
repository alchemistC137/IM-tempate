namespace MemByte.Infrastructure.Identity;

/// <summary>
/// Constantes de los roles del sistema, para evitar strings mágicos al
/// asignar roles o autorizar controladores/páginas.
/// </summary>
public static class ApplicationRoleNames
{
	public const string Admin = "Admin";
	public const string AreaManager = "AreaManager";
	public const string PurchasingAgent = "PurchasingAgent";
	public const string Support = "Support";
	public const string Employee = "Employee";

	public static readonly string[] All =
	{
		Admin,
		AreaManager,
		PurchasingAgent,
		Support,
		Employee
	};

	/// <summary>
	/// Roles autorizados a responder en el módulo de mensajería interna. El
	/// resto de usuarios puede leer sus conversaciones, pero no contestar.
	/// </summary>
	public static readonly string[] MessagingResponders =
	{
		Admin,
		Support
	};

	/// <summary>
	/// Roles autorizados a gestionar pedidos (alta, líneas, transiciones de
	/// estado y pago). El resto de usuarios no accede al módulo de pedidos.
	/// </summary>
	public static readonly string[] OrderManagers =
	{
		Admin,
		Employee
	};
}
