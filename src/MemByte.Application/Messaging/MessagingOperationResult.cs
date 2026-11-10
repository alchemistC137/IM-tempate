namespace MemByte.Application.Messaging;

/// <summary>
/// Resultado de una operación de mensajería: éxito con el dato resultante o
/// fallo con los errores de negocio/autorización. Los mensajes de error son
/// deliberadamente genéricos cuando la causa es un acceso denegado, para no
/// revelar la existencia de conversaciones ajenas.
/// </summary>
public sealed record MessagingOperationResult<T>(bool Succeeded, T? Value, IReadOnlyList<string> Errors)
{
	public static MessagingOperationResult<T> Success(T value) =>
		new(true, value, []);

	public static MessagingOperationResult<T> Failure(params string[] errors) =>
		new(false, default, errors);

	/// <summary>
	/// Fallo estándar por acceso denegado o inexistencia del recurso. Se usa
	/// el mismo texto en ambos casos para no filtrar si el hilo existe.
	/// </summary>
	public static MessagingOperationResult<T> NotAccessible() =>
		new(false, default, ["La conversación no existe o no tienes acceso a ella."]);
}
