namespace MemByte.Application.Auth;

/// <summary>
/// Resultado de fijar una contraseña a partir de un token de invitación o
/// de reseteo. Los errores incluyen tanto un token inválido/caducado como
/// el incumplimiento de la política de contraseñas: a diferencia del login,
/// aquí no hay riesgo de enumeración de cuentas (el enlace ya identifica al
/// usuario), así que se pueden mostrar mensajes concretos.
/// </summary>
public sealed record SetPasswordResult(bool Succeeded, IReadOnlyList<string> Errors)
{
	public static SetPasswordResult Success() =>
		new(true, []);

	public static SetPasswordResult Failure(params string[] errors) =>
		new(false, errors);
}
