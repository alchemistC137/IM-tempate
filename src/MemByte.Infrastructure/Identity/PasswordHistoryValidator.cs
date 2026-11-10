using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Identity;

/// <summary>
/// Valida que la nueva contraseña no coincida con ninguna de las últimas
/// <see cref="MaxHistoryEntries"/> contraseñas usadas por el usuario. Se
/// engancha en el pipeline estándar de <see cref="UserManager{TUser}"/> vía
/// <c>AddPasswordValidator</c>, por lo que se ejecuta tanto en
/// ChangePasswordAsync como en ResetPasswordAsync.
/// </summary>
public class PasswordHistoryValidator : IPasswordValidator<ApplicationUser>
{
	public const int MaxHistoryEntries = 5;

	private readonly Persistence.MemByteDbContext _dbContext;

	public PasswordHistoryValidator(Persistence.MemByteDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
	{
		if (string.IsNullOrEmpty(password))
			return IdentityResult.Success;

		var history = await _dbContext.Set<PasswordHistoryEntry>()
			.Where(h => h.UserId == user.Id)
			.OrderByDescending(h => h.CreatedAt)
			.Take(MaxHistoryEntries)
			.ToListAsync();

		foreach (var entry in history)
		{
			var verification = manager.PasswordHasher.VerifyHashedPassword(user, entry.PasswordHash, password);

			if (verification != PasswordVerificationResult.Failed)
			{
				return IdentityResult.Failed(new IdentityError
				{
					Code = "PasswordReused",
					Description = "No puedes reutilizar ninguna de tus últimas 5 contraseñas."
				});
			}
		}

		return IdentityResult.Success;
	}
}
