using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MemByte.Infrastructure.Identity;

/// <summary>
/// UserManager especializado que, tras un cambio o restablecimiento de
/// contraseña correcto, registra el hash anterior en el historial de
/// contraseñas y recorta el historial a <see cref="PasswordHistoryValidator.MaxHistoryEntries"/>
/// entradas por usuario.
/// </summary>
public class ApplicationUserManager : UserManager<ApplicationUser>
{
	private readonly Persistence.MemByteDbContext _dbContext;

	public ApplicationUserManager(
		IUserStore<ApplicationUser> store,
		IOptions<IdentityOptions> optionsAccessor,
		IPasswordHasher<ApplicationUser> passwordHasher,
		IEnumerable<IUserValidator<ApplicationUser>> userValidators,
		IEnumerable<IPasswordValidator<ApplicationUser>> passwordValidators,
		ILookupNormalizer keyNormalizer,
		IdentityErrorDescriber errors,
		IServiceProvider services,
		Microsoft.Extensions.Logging.ILogger<UserManager<ApplicationUser>> logger,
		Persistence.MemByteDbContext dbContext)
		: base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
	{
		_dbContext = dbContext;
	}

	public override async Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword)
	{
		var result = await base.ChangePasswordAsync(user, currentPassword, newPassword);

		if (result.Succeeded)
			await RecordPasswordHistoryAsync(user);

		return result;
	}

	public override async Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string token, string newPassword)
	{
		var result = await base.ResetPasswordAsync(user, token, newPassword);

		if (result.Succeeded)
			await RecordPasswordHistoryAsync(user);

		return result;
	}

	private async Task RecordPasswordHistoryAsync(ApplicationUser user)
	{
		// user.PasswordHash ya refleja el nuevo hash tras el ChangePasswordAsync/ResetPasswordAsync exitoso.
		if (string.IsNullOrEmpty(user.PasswordHash))
			return;

		_dbContext.Set<PasswordHistoryEntry>().Add(new PasswordHistoryEntry(user.Id, user.PasswordHash));

		var history = _dbContext.Set<PasswordHistoryEntry>()
			.Where(h => h.UserId == user.Id)
			.OrderByDescending(h => h.CreatedAt)
			.Skip(PasswordHistoryValidator.MaxHistoryEntries);

		_dbContext.RemoveRange(history);

		await _dbContext.SaveChangesAsync();
	}
}
