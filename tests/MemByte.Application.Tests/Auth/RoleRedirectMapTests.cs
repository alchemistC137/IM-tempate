using MemByte.Application.Auth;

namespace MemByte.Application.Tests.Auth;

public class RoleRedirectMapTests
{
	[Theory]
	[InlineData(new[] { "Admin" }, "/admin/dashboard")]
	[InlineData(new[] { "AreaManager" }, "/team/dashboard")]
	[InlineData(new[] { "PurchasingAgent" }, "/purchasing/dashboard")]
	[InlineData(new[] { "Employee" }, "/dashboard")]
	public void Resolve_ReturnsExpectedRouteForSingleRole(string[] roles, string expected)
	{
		var route = RoleRedirectMap.Resolve(roles);

		Assert.Equal(expected, route);
	}

	[Fact]
	public void Resolve_PrioritizesHighestPrivilegeRole_WhenUserHasMultipleRoles()
	{
		var route = RoleRedirectMap.Resolve(new[] { "Employee", "AreaManager" });

		Assert.Equal("/team/dashboard", route);
	}

	[Fact]
	public void Resolve_AdminTakesPriorityOverAllOtherRoles()
	{
		var route = RoleRedirectMap.Resolve(new[] { "Employee", "AreaManager", "PurchasingAgent", "Admin" });

		Assert.Equal("/admin/dashboard", route);
	}

	[Fact]
	public void Resolve_FallsBackToDefaultDashboard_WhenNoKnownRoleIsPresent()
	{
		var route = RoleRedirectMap.Resolve(Array.Empty<string>());

		Assert.Equal("/dashboard", route);
	}

	[Fact]
	public void Resolve_IsCaseInsensitive()
	{
		var route = RoleRedirectMap.Resolve(new[] { "admin" });

		Assert.Equal("/admin/dashboard", route);
	}
}
