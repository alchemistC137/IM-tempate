using System.ComponentModel.DataAnnotations;

namespace MemByte.Application.Configuration;

public sealed class IntranetOptions
{
	public const string SectionName = "Intranet";

	[Required, StringLength(100)]
	public string ApplicationName { get; set; } = "Intranet";

	[Required, StringLength(100)]
	public string CompanyName { get; set; } = "Mi empresa";
}
