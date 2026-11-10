using Microsoft.AspNetCore.Identity;

namespace MemByte.Infrastructure.Identity;

/// <summary>
/// Comprueba la contraseña propuesta contra una lista reducida de
/// contraseñas triviales/filtradas de uso extremadamente común. Este
/// validador es un marcador de posición local: la integración real con un
/// servicio externo (p.ej. la API "Pwned Passwords" de HaveIBeenPwned,
/// usando el modelo k-anonymity por prefijo de hash SHA-1) debe sustituir
/// esta lista antes de producción, dentro de la fase de hardening.
/// </summary>
public class BreachedPasswordValidator : IPasswordValidator<ApplicationUser>
{
	private static readonly HashSet<string> KnownBreachedPasswords = new(StringComparer.OrdinalIgnoreCase)
	{
		"password123!", "password1234", "qwerty123456", "letmein12345",
		"admin12345678", "welcome12345", "changeme1234", "123456789012"
	};

	public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
	{
		if (!string.IsNullOrEmpty(password) && KnownBreachedPasswords.Contains(password))
		{
			return Task.FromResult(IdentityResult.Failed(new IdentityError
			{
				Code = "PasswordBreached",
				Description = "Esta contraseña aparece en listas de contraseñas filtradas conocidas. Elige otra."
			}));
		}

		return Task.FromResult(IdentityResult.Success);
	}
}
