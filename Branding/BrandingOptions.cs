using MudBlazor;

namespace PWA.Branding;

/// <summary>
/// Public, non-secret visual configuration for the tenant hosting this PWA.
/// Values are populated from the deployment-specific wwwroot/appsettings.json.
/// </summary>
public sealed class BrandingOptions
{
    public string CompanyName { get; set; } = "Cofradía de Pescadores de Gandia";
    public string ApplicationTitle { get; set; } = "CRM";
    public string LogoPath { get; set; } = "imagenes/logo-cofradia.png";
    public string LoginImagePath { get; set; } = "imagenes/cartel-pegatinas-santos.png";
    public string HomeImagePath { get; set; } = "imagenes/cartel-pegatinas-santos.png";
    public string PrimaryColor { get; set; } = "#03173d";
    public string SecondaryColor { get; set; } = "#594ae2";
    public string ThemeColor { get; set; } = "#03173d";
    public bool ShowLoginImage { get; set; } = true;
    public bool ShowHomeImage { get; set; } = true;

    public MudTheme Theme => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = PrimaryColor,
            Secondary = SecondaryColor
        },
        PaletteDark = new PaletteDark
        {
            Primary = PrimaryColor,
            Secondary = SecondaryColor
        }
    };
}
