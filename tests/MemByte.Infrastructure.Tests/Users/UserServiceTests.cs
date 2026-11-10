using MemByte.Application.Notifications;
using MemByte.Application.Configuration;
using MemByte.Application.Users;
using MemByte.Domain.Clients;
using MemByte.Domain.Employees;
using MemByte.Infrastructure.Identity;
using MemByte.Infrastructure.Tests.Auth;
using MemByte.Infrastructure.Users;
using Microsoft.Extensions.Options;
using Xunit;

namespace MemByte.Infrastructure.Tests.Users;

public class UserServiceTests
{
	/// <summary>
	/// Doble de prueba de <see cref="IEmailSender"/> que solo registra los
	/// mensajes en memoria, para poder aserzar que el flujo de invitación/
	/// reseteo intenta enviar el email correcto sin depender de infraestructura
	/// real de correo.
	/// </summary>
	private sealed class RecordingEmailSender : IEmailSender
	{
		public List<EmailMessage> SentMessages { get; } = [];

		public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
		{
			SentMessages.Add(message);
			return Task.CompletedTask;
		}
	}

	private static UserService CreateSut(AuthTestHost host, IEmailSender? emailSender = null) =>
		new(host.UserManager, host.DbContext, emailSender ?? new RecordingEmailSender());

	[Theory]
	[InlineData("Intranet")]
	[InlineData("Portal de Empresa Norte")]
	public async Task AccountEmails_UseConfiguredApplicationName(string applicationName)
	{
		using var host = new AuthTestHost();
		var emailSender = new RecordingEmailSender();
		var options = Options.Create(new IntranetOptions { ApplicationName = applicationName });
		var sut = new UserService(host.UserManager, host.DbContext, emailSender, intranetOptions: options);

		var created = await sut.CreateAsync(new CreateUserRequest("person@example.test", "Persona", null, null, []));
		Assert.True(created.Succeeded);
		var reset = await sut.ResetPasswordAsync(created.User!.Id);

		Assert.True(reset.Succeeded);
		Assert.Equal(2, emailSender.SentMessages.Count);
		Assert.Equal($"Activa tu cuenta en {applicationName}", emailSender.SentMessages[0].Subject);
		Assert.Equal($"Restablece tu contraseña en {applicationName}", emailSender.SentMessages[1].Subject);
		Assert.All(emailSender.SentMessages, message =>
		{
			Assert.Contains(applicationName, message.Body);
			Assert.DoesNotContain("MemByte", message.Body);
		});
	}

	[Fact]
	public async Task GetAllAsync_ReturnsAllUsersOrderedByDisplayName()
	{
		using var host = new AuthTestHost();
		await host.CreateUserAsync("bob@membyte.test", AuthTestHost.ValidPassword);
		await host.CreateUserAsync("alice@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		var users = await sut.GetAllAsync();

		Assert.Equal(2, users.Count);
		Assert.Equal("alice@membyte.test", users[0].DisplayName);
		Assert.Equal("bob@membyte.test", users[1].DisplayName);
	}

	[Fact]
	public async Task GetByIdAsync_UnknownId_ReturnsNull()
	{
		using var host = new AuthTestHost();
		var sut = CreateSut(host);

		var result = await sut.GetByIdAsync(Guid.NewGuid());

		Assert.Null(result);
	}

	[Fact]
	public async Task GetByIdAsync_ExistingUser_ReturnsDtoWithRoles()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("admin@membyte.test", AuthTestHost.ValidPassword, ApplicationRoleNames.Admin);
		var sut = CreateSut(host);

		var dto = await sut.GetByIdAsync(user.Id);

		Assert.NotNull(dto);
		Assert.Equal("admin@membyte.test", dto!.Email);
		Assert.Contains(ApplicationRoleNames.Admin, dto.Roles);
		Assert.True(dto.IsActive);
	}

	[Fact]
	public async Task UpdateAsync_ValidRequest_UpdatesDisplayNameAndRoles()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("jane@membyte.test", AuthTestHost.ValidPassword);
		await host.RoleManager.CreateAsync(new ApplicationRole { Name = ApplicationRoleNames.AreaManager });
		var sut = CreateSut(host);

		var result = await sut.UpdateAsync(user.Id, new UpdateUserRequest(
			"Jane Doe",
			null,
			null,
			[ApplicationRoleNames.AreaManager],
			IsMfaRequired: true));

		Assert.True(result.Succeeded);
		Assert.Equal("Jane Doe", result.User!.DisplayName);
		Assert.Contains(ApplicationRoleNames.AreaManager, result.User.Roles);
		Assert.True(result.User.IsMfaRequired);
	}

	[Fact]
	public async Task UpdateAsync_LinksEmployeeAndClient()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("linked@membyte.test", AuthTestHost.ValidPassword);

		var employee = new Employee("Ana", "Perez", "ana@membyte.test", "IT", EmployeeRoles.Employee, DateOnly.FromDateTime(DateTime.Today));
		var client = new Client("Acme Corp");
		host.DbContext.Employees.Add(employee);
		host.DbContext.Clients.Add(client);
		await host.DbContext.SaveChangesAsync();

		var sut = CreateSut(host);

		var result = await sut.UpdateAsync(user.Id, new UpdateUserRequest(
			"Linked User",
			employee.Id,
			client.Id,
			[],
			IsMfaRequired: false));

		Assert.True(result.Succeeded);
		Assert.Equal(employee.Id, result.User!.EmployeeId);
		Assert.Equal(employee.FullName, result.User.EmployeeFullName);
		Assert.Equal(client.Id, result.User.ClientId);
		Assert.Equal(client.Name, result.User.ClientName);
	}

	[Fact]
	public async Task UpdateAsync_UnknownEmployeeId_Fails()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("nolink@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		var result = await sut.UpdateAsync(user.Id, new UpdateUserRequest(
			"No Link", Guid.NewGuid(), null, [], false));

		Assert.False(result.Succeeded);
		Assert.Contains(result.Errors, e => e.Contains("empleado"));
	}

	[Fact]
	public async Task UpdateAsync_UnknownClientId_Fails()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("nolink2@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		var result = await sut.UpdateAsync(user.Id, new UpdateUserRequest(
			"No Link", null, Guid.NewGuid(), [], false));

		Assert.False(result.Succeeded);
		Assert.Contains(result.Errors, e => e.Contains("cliente"));
	}

	[Fact]
	public async Task UpdateAsync_InvalidRole_Fails()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("badrole@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		var result = await sut.UpdateAsync(user.Id, new UpdateUserRequest(
			"Bad Role", null, null, ["NotARole"], false));

		Assert.False(result.Succeeded);
		Assert.Contains(result.Errors, e => e.Contains("NotARole"));
	}

	[Fact]
	public async Task UpdateAsync_UnknownUser_Fails()
	{
		using var host = new AuthTestHost();
		var sut = CreateSut(host);

		var result = await sut.UpdateAsync(Guid.NewGuid(), new UpdateUserRequest("X", null, null, [], false));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task SetActiveAsync_Deactivate_SetsLockoutSentinelAndRevokesSessions()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("deactivate@membyte.test", AuthTestHost.ValidPassword);
		var session = new UserSession(user.Id, "token-1", "127.0.0.1", "test-agent");
		host.DbContext.UserSessions.Add(session);
		await host.DbContext.SaveChangesAsync();

		var sut = CreateSut(host);

		var result = await sut.SetActiveAsync(user.Id, isActive: false);

		Assert.True(result.Succeeded);
		Assert.False(result.User!.IsActive);

		var reloaded = await host.UserManager.FindByIdAsync(user.Id.ToString());
		Assert.Equal(DateTimeOffset.MaxValue, reloaded!.LockoutEnd);

		var reloadedSession = await host.DbContext.UserSessions.FindAsync(session.Id);
		Assert.True(reloadedSession!.IsRevoked);
	}

	[Fact]
	public async Task SetActiveAsync_Reactivate_ClearsLockoutEnd()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("reactivate@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		await sut.SetActiveAsync(user.Id, isActive: false);
		var result = await sut.SetActiveAsync(user.Id, isActive: true);

		Assert.True(result.Succeeded);
		Assert.True(result.User!.IsActive);

		var reloaded = await host.UserManager.FindByIdAsync(user.Id.ToString());
		Assert.Null(reloaded!.LockoutEnd);
	}

	[Fact]
	public async Task RevokeSessionsAsync_MarksSessionsRevokedAndBumpsSecurityStamp()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("revoke@membyte.test", AuthTestHost.ValidPassword);
		var originalStamp = user.SecurityStamp;

		var session = new UserSession(user.Id, "token-2", null, null);
		host.DbContext.UserSessions.Add(session);
		await host.DbContext.SaveChangesAsync();

		var sut = CreateSut(host);

		var result = await sut.RevokeSessionsAsync(user.Id);

		Assert.True(result.Succeeded);

		var reloadedSession = await host.DbContext.UserSessions.FindAsync(session.Id);
		Assert.True(reloadedSession!.IsRevoked);

		var reloadedUser = await host.UserManager.FindByIdAsync(user.Id.ToString());
		Assert.NotEqual(originalStamp, reloadedUser!.SecurityStamp);
	}

	[Fact]
	public async Task RevokeSessionsAsync_UnknownUser_Fails()
	{
		using var host = new AuthTestHost();
		var sut = CreateSut(host);

		var result = await sut.RevokeSessionsAsync(Guid.NewGuid());

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task CreateAsync_ValidRequest_CreatesUserWithoutUsablePasswordAndSendsInvitation()
	{
		using var host = new AuthTestHost();
		var emailSender = new RecordingEmailSender();
		await host.RoleManager.CreateAsync(new ApplicationRole { Name = ApplicationRoleNames.Employee });
		var sut = CreateSut(host, emailSender);

		var result = await sut.CreateAsync(new CreateUserRequest(
			"newuser@membyte.test", "New User", null, null, [ApplicationRoleNames.Employee]));

		Assert.True(result.Succeeded);
		Assert.NotNull(result.ActionUrlPath);
		Assert.StartsWith("/account/set-password?userId=", result.ActionUrlPath);
		Assert.Contains(ApplicationRoleNames.Employee, result.User!.Roles);

		var createdUser = await host.UserManager.FindByEmailAsync("newuser@membyte.test");
		Assert.NotNull(createdUser);
		Assert.Null(createdUser!.PasswordHash);

		Assert.Single(emailSender.SentMessages);
		Assert.Equal("newuser@membyte.test", emailSender.SentMessages[0].To);
	}

	[Fact]
	public async Task CreateAsync_LinksEmployeeAndClient()
	{
		using var host = new AuthTestHost();
		var employee = new Employee("Luis", "Gomez", "luis@membyte.test", "IT", EmployeeRoles.Employee, DateOnly.FromDateTime(DateTime.Today));
		var client = new Client("Beta Corp");
		host.DbContext.Employees.Add(employee);
		host.DbContext.Clients.Add(client);
		await host.DbContext.SaveChangesAsync();

		var sut = CreateSut(host);

		var result = await sut.CreateAsync(new CreateUserRequest(
			"linked-new@membyte.test", "Linked New", employee.Id, client.Id, []));

		Assert.True(result.Succeeded);
		Assert.Equal(employee.Id, result.User!.EmployeeId);
		Assert.Equal(client.Id, result.User.ClientId);
	}

	[Fact]
	public async Task CreateAsync_DuplicateEmail_Fails()
	{
		using var host = new AuthTestHost();
		await host.CreateUserAsync("dup@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		var result = await sut.CreateAsync(new CreateUserRequest("dup@membyte.test", "Dup", null, null, []));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task CreateAsync_UnknownEmployeeId_Fails()
	{
		using var host = new AuthTestHost();
		var sut = CreateSut(host);

		var result = await sut.CreateAsync(new CreateUserRequest(
			"orphan@membyte.test", "Orphan", Guid.NewGuid(), null, []));

		Assert.False(result.Succeeded);
		Assert.Contains(result.Errors, e => e.Contains("empleado"));
	}

	[Fact]
	public async Task CreateAsync_InvalidRole_Fails()
	{
		using var host = new AuthTestHost();
		var sut = CreateSut(host);

		var result = await sut.CreateAsync(new CreateUserRequest(
			"badrole-new@membyte.test", "Bad Role", null, null, ["NotARole"]));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task ResetPasswordAsync_ExistingUser_GeneratesActionLinkAndSendsEmail()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("resetme@membyte.test", AuthTestHost.ValidPassword);
		var emailSender = new RecordingEmailSender();
		var sut = CreateSut(host, emailSender);

		var result = await sut.ResetPasswordAsync(user.Id);

		Assert.True(result.Succeeded);
		Assert.NotNull(result.ActionUrlPath);
		Assert.Contains($"userId={user.Id}", result.ActionUrlPath);
		Assert.Single(emailSender.SentMessages);
		Assert.Equal("resetme@membyte.test", emailSender.SentMessages[0].To);
	}

	[Fact]
	public async Task ResetPasswordAsync_UnknownUser_Fails()
	{
		using var host = new AuthTestHost();
		var sut = CreateSut(host);

		var result = await sut.ResetPasswordAsync(Guid.NewGuid());

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task CreateAsync_ValidRequest_RecordsUserCreatedByAdminAuditEntry()
	{
		using var host = new AuthTestHost();
		var sut = CreateSut(host);

		var result = await sut.CreateAsync(new CreateUserRequest("audited-new@membyte.test", "Audited New", null, null, []));

		Assert.True(result.Succeeded);
		Assert.Contains(host.DbContext.AuthAuditEntries, e =>
			e.UserId == result.User!.Id && e.EventType == "UserCreatedByAdmin" && e.WasSuccessful);
	}

	[Fact]
	public async Task UpdateAsync_WhenRolesChange_RecordsUserRoleChangedAuditEntry()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("rolechange@membyte.test", AuthTestHost.ValidPassword);
		await host.RoleManager.CreateAsync(new ApplicationRole { Name = ApplicationRoleNames.AreaManager });
		var sut = CreateSut(host);

		var result = await sut.UpdateAsync(user.Id, new UpdateUserRequest(
			"Role Change", null, null, [ApplicationRoleNames.AreaManager], false));

		Assert.True(result.Succeeded);
		Assert.Contains(host.DbContext.AuthAuditEntries, e =>
			e.UserId == user.Id && e.EventType == "UserRoleChanged" && e.WasSuccessful);
	}

	[Fact]
	public async Task UpdateAsync_WhenRolesUnchanged_DoesNotRecordUserRoleChangedAuditEntry()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("norolechange@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		var result = await sut.UpdateAsync(user.Id, new UpdateUserRequest("No Role Change", null, null, [], false));

		Assert.True(result.Succeeded);
		Assert.DoesNotContain(host.DbContext.AuthAuditEntries, e => e.UserId == user.Id && e.EventType == "UserRoleChanged");
	}

	[Fact]
	public async Task SetActiveAsync_Deactivate_RecordsUserDeactivatedAuditEntry()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("deactivate-audit@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		await sut.SetActiveAsync(user.Id, isActive: false);

		Assert.Contains(host.DbContext.AuthAuditEntries, e =>
			e.UserId == user.Id && e.EventType == "UserDeactivated" && e.WasSuccessful);
	}

	[Fact]
	public async Task SetActiveAsync_Reactivate_RecordsUserReactivatedAuditEntry()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("reactivate-audit@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		await sut.SetActiveAsync(user.Id, isActive: false);
		await sut.SetActiveAsync(user.Id, isActive: true);

		Assert.Contains(host.DbContext.AuthAuditEntries, e =>
			e.UserId == user.Id && e.EventType == "UserReactivated" && e.WasSuccessful);
	}

	[Fact]
	public async Task RevokeSessionsAsync_RecordsSessionsRevokedByAdminAuditEntry()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("revoke-audit@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		await sut.RevokeSessionsAsync(user.Id);

		Assert.Contains(host.DbContext.AuthAuditEntries, e =>
			e.UserId == user.Id && e.EventType == "SessionsRevokedByAdmin" && e.WasSuccessful);
	}

	[Fact]
	public async Task ResetPasswordAsync_RecordsPasswordResetRequestedByAdminAuditEntry()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("reset-audit@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		await sut.ResetPasswordAsync(user.Id);

		Assert.Contains(host.DbContext.AuthAuditEntries, e =>
			e.UserId == user.Id && e.EventType == "PasswordResetRequestedByAdmin" && e.WasSuccessful);
	}

	[Fact]
	public async Task GetAuditHistoryAsync_ReturnsEntriesOrderedByMostRecentFirst()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("history@membyte.test", AuthTestHost.ValidPassword);
		var sut = CreateSut(host);

		await sut.RevokeSessionsAsync(user.Id);
		await Task.Delay(5);
		await sut.SetActiveAsync(user.Id, isActive: false);
		await Task.Delay(5);
		await sut.SetActiveAsync(user.Id, isActive: true);

		var history = await sut.GetAuditHistoryAsync(user.Id);

		Assert.True(history.Count >= 3);
		Assert.Equal("UserReactivated", history[0].EventType);
		for (var i = 0; i < history.Count - 1; i++)
			Assert.True(history[i].OccurredAt >= history[i + 1].OccurredAt);
	}

	[Fact]
	public async Task GetAuditHistoryAsync_UnknownUser_ReturnsEmptyList()
	{
		using var host = new AuthTestHost();
		var sut = CreateSut(host);

		var history = await sut.GetAuditHistoryAsync(Guid.NewGuid());

		Assert.Empty(history);
	}
}
