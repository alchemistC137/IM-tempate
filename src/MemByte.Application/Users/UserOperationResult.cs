namespace MemByte.Application.Users;

/// <summary>
/// Resultado de una operación sobre una cuenta de usuario (alta, edición,
/// activación/desactivación, reseteo de contraseña, revocación de
/// sesiones): éxito con el DTO resultante, o fallo con los errores de
/// negocio para mostrarlos en la UI.
/// </summary>
/// <param name="ActionUrlPath">
/// Ruta relativa (sin dominio) de la pantalla de activación/fijado de
/// contraseña, solo presente en el resultado de <c>CreateAsync</c> y
/// <c>ResetPasswordAsync</c>. Mientras no haya un proveedor de email real
/// (ver <c>LoggingEmailSender</c>), la UI la muestra al Admin como enlace de
/// un solo uso para que la transmita de forma segura al usuario.
/// </param>
public sealed record UserOperationResult(bool Succeeded, UserDto? User, IReadOnlyList<string> Errors, string? ActionUrlPath = null)
{
	public static UserOperationResult Success(UserDto user, string? actionUrlPath = null) =>
		new(true, user, [], actionUrlPath);

	public static UserOperationResult Failure(params string[] errors) =>
		new(false, null, errors);
}
