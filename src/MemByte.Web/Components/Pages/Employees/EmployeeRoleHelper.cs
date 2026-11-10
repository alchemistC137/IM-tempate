namespace MemByte.Web.Components.Pages.Employees;

/// <summary>
/// Helpers compartidos para el módulo de empleados.
/// </summary>
public static class EmployeeRoleHelper
{
	public static string DisplayName(string role) => role switch
	{
		"Admin" => "Admin",
		"AreaManager" => "Jefe de área",
		"PurchasingAgent" => "Ag. compras",
		"Employee" => "Empleado",
		_ => role
	};

	public static string BadgeClass(string role) => role switch
	{
		"Admin" => "bg-danger-subtle text-danger",
		"AreaManager" => "bg-primary-subtle text-primary",
		"PurchasingAgent" => "bg-warning-subtle text-warning",
		_ => "bg-info-subtle text-info"
	};
}
