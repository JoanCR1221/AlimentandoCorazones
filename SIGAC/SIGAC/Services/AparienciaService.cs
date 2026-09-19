using Microsoft.JSInterop;
using MudBlazor;

namespace SIGAC.Services;

public sealed class AparienciaService : IAsyncDisposable
{
    // Rango del control deslizante de tamaño de letra. Porcentaje aplicado
    // directo como font-size del <html> (ver apariencia.js): 100 es el tamaño
    // del navegador sin ajustar, no un valor de diseño arbitrario.
    public const int TamanoFuenteMinimo = 80;
    public const int TamanoFuenteMaximo = 150;
    public const int TamanoFuentePredeterminado = 100;

    private const string StorageKey = "sigac-apariencia";
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private bool _initialized;

    public AparienciaService(IJSRuntime js)
    {
        _js = js;
    }

    public bool EsOscuro { get; private set; }
    public bool AltoContraste { get; private set; }
    public int TamanoFuente { get; private set; } = TamanoFuentePredeterminado;

    public MudTheme Tema => ConstruirTema();

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        var module = await GetModuleAsync();
        var settings = await module.InvokeAsync<AparienciaSettings?>("loadSettings", StorageKey);

        if (settings is not null)
        {
            EsOscuro = settings.EsOscuro;
            AltoContraste = settings.AltoContraste;

            // Acotado y no asignado directo: una preferencia guardada por una
            // versión anterior (o corrompida a mano en localStorage) no puede
            // dejar el texto ilegible por fuera del rango que el slider ofrece.
            TamanoFuente = Math.Clamp(settings.TamanoFuente, TamanoFuenteMinimo, TamanoFuenteMaximo);
        }

        await AplicarAlDocumentoAsync();
        _initialized = true;
        OnChange?.Invoke();
    }

    public async Task SetEsOscuroAsync(bool esOscuro)
    {
        if (EsOscuro == esOscuro)
        {
            return;
        }

        EsOscuro = esOscuro;
        await GuardarYAplicarAsync();
    }

    public async Task SetAltoContrasteAsync(bool altoContraste)
    {
        if (AltoContraste == altoContraste)
        {
            return;
        }

        AltoContraste = altoContraste;
        await GuardarYAplicarAsync();
    }

    public async Task SetTamanoFuenteAsync(int porcentaje)
    {
        var acotado = Math.Clamp(porcentaje, TamanoFuenteMinimo, TamanoFuenteMaximo);
        if (TamanoFuente == acotado)
        {
            return;
        }

        TamanoFuente = acotado;
        await GuardarYAplicarAsync();
    }

    private async Task GuardarYAplicarAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("saveSettings", StorageKey, new AparienciaSettings
        {
            EsOscuro = EsOscuro,
            AltoContraste = AltoContraste,
            TamanoFuente = TamanoFuente
        });
        await AplicarAlDocumentoAsync();
        OnChange?.Invoke();
    }

    private async Task AplicarAlDocumentoAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("applyAppearance", new AparienciaSettings
        {
            EsOscuro = EsOscuro,
            AltoContraste = AltoContraste,
            TamanoFuente = TamanoFuente
        });
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/apariencia.js");
        return _module;
    }

    // Tipografía de marca: Outfit para títulos (H1-H6) y DM Sans para el resto.
    // Se declara la familia en cada nivel y no solo en Default: MudBlazor genera
    // una variable CSS de familia por nivel tipográfico y, si un nivel no la
    // trae, cae en su propio valor por defecto (Roboto), no en el de Default.
    private static readonly string[] FuenteTitulos = { "Outfit", "DM Sans", "system-ui", "sans-serif" };
    private static readonly string[] FuenteCuerpo = { "DM Sans", "system-ui", "-apple-system", "sans-serif" };

    private static Typography ConstruirTipografia() => new()
    {
        Default = new DefaultTypography { FontFamily = FuenteCuerpo },
        H1 = new H1Typography { FontFamily = FuenteTitulos },
        H2 = new H2Typography { FontFamily = FuenteTitulos },
        H3 = new H3Typography { FontFamily = FuenteTitulos },
        H4 = new H4Typography { FontFamily = FuenteTitulos },
        H5 = new H5Typography { FontFamily = FuenteTitulos },
        H6 = new H6Typography { FontFamily = FuenteTitulos },
        Subtitle1 = new Subtitle1Typography { FontFamily = FuenteCuerpo },
        Subtitle2 = new Subtitle2Typography { FontFamily = FuenteCuerpo },
        Body1 = new Body1Typography { FontFamily = FuenteCuerpo },
        Body2 = new Body2Typography { FontFamily = FuenteCuerpo },
        Button = new ButtonTypography { FontFamily = FuenteCuerpo },
        Caption = new CaptionTypography { FontFamily = FuenteCuerpo },
        Overline = new OverlineTypography { FontFamily = FuenteCuerpo }
    };

    private MudTheme ConstruirTema()
    {
        if (AltoContraste)
        {
            // Misma identidad de marca (coral / verde azulado / lima), llevada
            // al extremo de contraste que este modo exige: fondo y texto casi
            // blanco puro / negro puro y los tonos de marca oscurecidos hasta
            // AAA (≥7:1) con texto blanco encima. Radios rectos y sin
            // animaciones se mantienen intactos (ver app.css /
            // MainLayout.razor.css, todo guardado con
            // :not([data-alto-contraste="true"])).
            return new MudTheme
            {
                PaletteLight = new PaletteLight
                {
                    // #7B240E con texto blanco da 10.00:1 (el coral-700 del tema
                    // normal, #C2401F, solo llega a 5.19:1: alcanza para AA, no
                    // para el AAA que busca este modo). Supera el 9.96:1 del
                    // "oxblood" anterior.
                    Primary = "#7B240E",
                    PrimaryContrastText = "#FFFFFF",
                    // #0B4A3E con blanco: 10.18:1.
                    Secondary = "#0B4A3E",
                    SecondaryContrastText = "#FFFFFF",
                    // #464D00 (olivo derivado del lima) con blanco: 9.04:1.
                    Tertiary = "#464D00",
                    TertiaryContrastText = "#FFFFFF",
                    // #0D4F66 con blanco: 9.02:1.
                    Info = "#0D4F66",
                    InfoContrastText = "#FFFFFF",
                    Success = "#0B4A3E",
                    SuccessContrastText = "#FFFFFF",
                    // #5A3400 con blanco: 10.90:1.
                    Warning = "#5A3400",
                    WarningContrastText = "#FFFFFF",
                    // #8C1610 con blanco: 9.39:1.
                    Error = "#8C1610",
                    ErrorContrastText = "#FFFFFF",
                    AppbarBackground = "#7B240E",
                    AppbarText = "#FFFFFF",
                    Background = "#FFFDFA",
                    Surface = "#FFFFFF",
                    DrawerBackground = "#FFFFFF",
                    DrawerText = "#000000",
                    DrawerIcon = "#7B240E",
                    TextPrimary = "#000000",
                    TextSecondary = "#000000",
                    TextDisabled = "#424242",
                    LinesDefault = "#000000",
                    LinesInputs = "#000000",
                    Divider = "#000000",
                    ActionDefault = "#000000",
                    TableLines = "#000000",
                    TableStriped = "#F5EDE4",
                    TableHover = "#EDE0D2"
                },
                PaletteDark = new PaletteDark
                {
                    // Coral y verde azulado claros sobre negro puro, con texto
                    // negro encima (todos ≥12:1).
                    Primary = "#FFAB91",
                    PrimaryContrastText = "#000000",
                    Secondary = "#6FE0D3",
                    SecondaryContrastText = "#000000",
                    Tertiary = "#E4EC4A",
                    TertiaryContrastText = "#000000",
                    Info = "#8EDCF2",
                    InfoContrastText = "#000000",
                    Success = "#6FE0A8",
                    SuccessContrastText = "#000000",
                    Warning = "#FFCC80",
                    WarningContrastText = "#000000",
                    Error = "#FF9088",
                    ErrorContrastText = "#000000",
                    AppbarBackground = "#000000",
                    AppbarText = "#FFFFFF",
                    Background = "#000000",
                    Surface = "#0A0A0A",
                    DrawerBackground = "#0A0A0A",
                    DrawerText = "#FFFFFF",
                    DrawerIcon = "#FFAB91",
                    TextPrimary = "#FFFFFF",
                    TextSecondary = "#FFFFFF",
                    TextDisabled = "#C4C4C4",
                    LinesDefault = "#FFFFFF",
                    LinesInputs = "#FFFFFF",
                    Divider = "#FFFFFF",
                    ActionDefault = "#FFFFFF",
                    TableLines = "#FFFFFF",
                    TableStriped = "#141414",
                    TableHover = "#242424"
                },
                Typography = ConstruirTipografia(),
                LayoutProperties = new LayoutProperties
                {
                    DefaultBorderRadius = "4px",
                    AppbarHeight = "64px"
                }
            };
        }

        return new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                // Identidad visual de Alimentando Corazones, tomada del logo:
                // coral, verde azulado y lima sobre crema.
                // Primary/AppBar usan coral-700 (#C2401F) y no el coral puro del
                // logo (#E5411C): con texto blanco encima el coral puro da
                // 4.11:1 (no llega a AA) y coral-700 da 5.19:1. El coral puro
                // queda para acentos (iconos, títulos grandes, bordes; ver
                // --ac-coral en app.css).
                Primary = "#C2401F",
                PrimaryContrastText = "#FFFFFF",
                // teal-700 (#17705F): 5.97:1 con blanco. El verde azulado del
                // logo (#219B8C) da 3.42:1, válido solo para texto grande/iconos.
                Secondary = "#17705F",
                SecondaryContrastText = "#FFFFFF",
                // Olivo derivado del lima del logo (#D2DA09 no admite texto
                // encima ni sirve como texto sobre blanco: 1.52:1). #5F6800:
                // 6.06:1 con blanco.
                Tertiary = "#5F6800",
                TertiaryContrastText = "#FFFFFF",
                // Azul verdoso, 5.97:1 con blanco. El azul por defecto de
                // MudBlazor (#2196F3) da 3.1:1.
                Info = "#1B6B8A",
                InfoContrastText = "#FFFFFF",
                Success = "#17705F",
                SuccessContrastText = "#FFFFFF",
                // Ámbar oscuro, 5.43:1 con blanco.
                Warning = "#9A5B00",
                WarningContrastText = "#FFFFFF",
                // 6.54:1 con blanco (el rojo por defecto da 3.68:1).
                Error = "#B3261E",
                ErrorContrastText = "#FFFFFF",
                AppbarBackground = "#C2401F",
                AppbarText = "#FFFFFF",
                Background = "#FFF8F2",
                Surface = "#FFFFFF",
                TextPrimary = "#262B2A",       // 13.66:1 sobre el crema
                TextSecondary = "#545B59",     // 6.61:1 sobre el crema
                DrawerBackground = "#FFFFFF",
                DrawerText = "#262B2A",
                DrawerIcon = "#C2401F",
                ActionDefault = "#545B59",
                // Borde de campos: 4.54:1 sobre blanco (elementos de UI ≥3:1).
                LinesInputs = "#6F7876",
                LinesDefault = "#E4D8CE",
                Divider = "#E4D8CE",
                TableLines = "#E4D8CE",
                TableStriped = "#FBF1E9",
                TableHover = "#F8E6DA"
            },
            PaletteDark = new PaletteDark
            {
                // Modo oscuro: negro puro de fondo, superficies #1F1F1F y
                // coral claro (#FF8A65) como Primary para que contraste sobre
                // negro (9.08:1). Con esos tonos claros el texto de los botones
                // rellenos pasa a negro (el blanco daría ~2.2:1).
                Primary = "#FF8A65",
                PrimaryContrastText = "#000000",
                Secondary = "#35BDB1",         // 9.07:1 sobre negro
                SecondaryContrastText = "#000000",
                Tertiary = "#D2DA09",          // lima: 13.77:1 sobre negro
                TertiaryContrastText = "#000000",
                Info = "#5CC8E8",
                InfoContrastText = "#000000",
                Success = "#3CC286",
                SuccessContrastText = "#000000",
                Warning = "#FFB74D",
                WarningContrastText = "#000000",
                Error = "#FF6B5E",             // 7.52:1 sobre negro, 5.90:1 sobre #1F1F1F
                ErrorContrastText = "#000000",
                AppbarBackground = "#1F1F1F",
                AppbarText = "#FFFFFF",
                Background = "#000000",
                Surface = "#1F1F1F",
                TextPrimary = "#FFFFFF",       // 16.48:1 sobre #1F1F1F
                TextSecondary = "#B8BDBB",     // 8.66:1 sobre #1F1F1F
                DrawerBackground = "#1F1F1F",
                DrawerText = "#FFFFFF",
                DrawerIcon = "#FF8A65",
                ActionDefault = "#B8BDBB",
                // Borde de campos: 5.31:1 sobre #1F1F1F.
                LinesInputs = "#8C9492",
                LinesDefault = "#3A3A3A",
                Divider = "#3A3A3A",
                TableLines = "#3A3A3A",
                TableStriped = "#141414",
                TableHover = "#2A2A2A"
            },
            Typography = ConstruirTipografia(),
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "12px",
                AppbarHeight = "64px"
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    private sealed class AparienciaSettings
    {
        public bool EsOscuro { get; set; }
        public bool AltoContraste { get; set; }
        public int TamanoFuente { get; set; } = TamanoFuentePredeterminado;
    }
}
