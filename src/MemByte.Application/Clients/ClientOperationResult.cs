namespace MemByte.Application.Clients;

/// <summary>
/// Resultado de una operación de alta/edición de clientes: éxito con el
/// DTO resultante, o fallo con los errores de validación de negocio (por
/// ejemplo, nombre duplicado) para mostrarlos en el formulario.
/// </summary>
public sealed record ClientOperationResult(bool Succeeded, ClientDto? Client, IReadOnlyList<string> Errors)
{
	public static ClientOperationResult Success(ClientDto client) =>
		new(true, client, []);

	public static ClientOperationResult Failure(params string[] errors) =>
		new(false, null, errors);
}
