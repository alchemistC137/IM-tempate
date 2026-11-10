using MudBlazor;

namespace MemByte.Web.Components;

/// <summary>
/// Tema visual de MemByte para MudBlazor: paleta corporativa profesional
/// (azul marino + acento) usada de forma consistente en toda la intranet,
/// empezando por el login.
/// </summary>
public static class MemByteTheme
{
	public static readonly MudTheme Theme = new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = "#0B3D91",
			Secondary = "#1F8A70",
			AppbarBackground = "#0B3D91",
			Background = "#F4F6F8",
			Surface = "#FFFFFF",
			DrawerBackground = "#0B3D91",
			DrawerText = "#E8ECF3"
		},
		PaletteDark = new PaletteDark
		{
			Primary = "#3B6FD6",
			Secondary = "#33C29A"
		},
		Typography = new Typography
		{
			Default = new DefaultTypography
			{
				FontFamily = new[] { "Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif" }
			}
		}
	};
}
