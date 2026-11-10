namespace MemByte.Application.Orders;

/// <summary>
/// Resultado genérico de una operación de alta/edición sobre un pedido:
/// éxito con el valor resultante, o fallo con los errores de validación de
/// negocio para mostrarlos en el formulario. Sigue el mismo patrón que
/// <c>CatalogOperationResult</c>.
/// </summary>
public sealed record OrderOperationResult<T>(bool Succeeded, T? Value, IReadOnlyList<string> Errors)
{
	public static OrderOperationResult<T> Success(T value) =>
		new(true, value, []);

	public static OrderOperationResult<T> Failure(params string[] errors) =>
		new(false, default, errors);
}
