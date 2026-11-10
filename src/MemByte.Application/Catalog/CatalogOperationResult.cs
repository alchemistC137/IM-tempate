namespace MemByte.Application.Catalog;

/// <summary>
/// Resultado genérico de una operación de alta/edición sobre el catálogo:
/// éxito con el valor resultante, o fallo con los errores de validación de
/// negocio para mostrarlos en el formulario. Sigue el mismo patrón que
/// <c>ClientOperationResult</c>, generalizado para reutilizarse en
/// categorías, productos y servicios.
/// </summary>
public sealed record CatalogOperationResult<T>(bool Succeeded, T? Value, IReadOnlyList<string> Errors)
{
	public static CatalogOperationResult<T> Success(T value) =>
		new(true, value, []);

	public static CatalogOperationResult<T> Failure(params string[] errors) =>
		new(false, default, errors);
}
