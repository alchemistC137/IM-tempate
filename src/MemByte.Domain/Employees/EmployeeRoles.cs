namespace MemByte.Domain.Employees;

/// <summary>
/// Rol organizativo (puesto) de la ficha de un empleado. No debe
/// confundirse con los roles de autorización de <c>ApplicationUser</c>
/// (Identity): un <see cref="Employee"/> es la ficha del trabajador, no la
/// cuenta con la que accede a la intranet. Se definen aquí, en Domain, para
/// no acoplar esta entidad a la capa de Infrastructure/Identity.
/// </summary>
public static class EmployeeRoles
{
	public const string Admin = "Admin";
	public const string AreaManager = "AreaManager";
	public const string PurchasingAgent = "PurchasingAgent";
	public const string Support = "Support";
	public const string Employee = "Employee";

	public static readonly string[] All =
	[
		Admin,
		AreaManager,
		PurchasingAgent,
		Support,
		Employee
	];

	public static bool IsValid(string role) => All.Contains(role);
}
