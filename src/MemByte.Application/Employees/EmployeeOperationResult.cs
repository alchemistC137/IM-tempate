namespace MemByte.Application.Employees;

/// <summary>
/// Resultado de una operación de alta/edición de empleados: éxito con el
/// DTO resultante, o fallo con los errores de validación de negocio (por
/// ejemplo, email duplicado) para mostrarlos en el formulario.
/// </summary>
public sealed record EmployeeOperationResult(bool Succeeded, EmployeeDto? Employee, IReadOnlyList<string> Errors)
{
	public static EmployeeOperationResult Success(EmployeeDto employee) =>
		new(true, employee, []);

	public static EmployeeOperationResult Failure(params string[] errors) =>
		new(false, null, errors);
}
