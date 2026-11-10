using MemByte.Application.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MemByte.Infrastructure.Tests.Configuration;

public class IntranetOptionsTests
{
	[Fact]
	public void MissingConfiguration_UsesNeutralDefaults()
	{
		using var services = CreateServices(new Dictionary<string, string?>());

		var options = services.GetRequiredService<IOptions<IntranetOptions>>().Value;

		Assert.Equal("Intranet", options.ApplicationName);
		Assert.Equal("Mi empresa", options.CompanyName);
	}

	[Fact]
	public void Configuration_BindsCompanyIdentity()
	{
		using var services = CreateServices(new Dictionary<string, string?>
		{
			["Intranet:ApplicationName"] = "Portal del equipo",
			["Intranet:CompanyName"] = "Empresa Norte"
		});

		var options = services.GetRequiredService<IOptions<IntranetOptions>>().Value;

		Assert.Equal("Portal del equipo", options.ApplicationName);
		Assert.Equal("Empresa Norte", options.CompanyName);
	}

	[Theory]
	[InlineData("ApplicationName", "")]
	[InlineData("ApplicationName", "   ")]
	[InlineData("CompanyName", "")]
	[InlineData("CompanyName", "   ")]
	[InlineData("ApplicationName", null)]
	[InlineData("CompanyName", null)]
	public void InvalidIdentity_IsRejected(string property, string? value)
	{
		using var services = CreateServices(new Dictionary<string, string?>
		{
			[$"Intranet:{property}"] = value
		});

		Assert.Throws<OptionsValidationException>(() =>
			services.GetRequiredService<IOptions<IntranetOptions>>().Value);
	}

	private static ServiceProvider CreateServices(Dictionary<string, string?> values)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
		var services = new ServiceCollection();
		services.AddOptions<IntranetOptions>()
			.Bind(configuration.GetSection(IntranetOptions.SectionName))
			.ValidateDataAnnotations();
		return services.BuildServiceProvider();
	}
}
