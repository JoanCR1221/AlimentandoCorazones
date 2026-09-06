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

    private MudTheme ConstruirTema()
    {
        if (AltoContraste)
        {
            return new MudTheme
            {
                PaletteLight = new PaletteLight
                {
                    Primary = "#004D40",
                    Secondary = "#1A237E",
                    AppbarBackground = "#004D40",
                    AppbarText = "#FFFFFF",
                    Background = "#FFFFFF",
                    Surface = "#FFFFFF",
                    TextPrimary = "#000000",
                    TextSecondary = "#000000",
                    TextDisabled = "#424242",
                    LinesDefault = "#000000",
                    Divider = "#000000",
                    ActionDefault = "#000000",
                    TableLines = "#000000",
                    TableStriped = "#F0F0F0",
                    TableHover = "#E8E8E8"
                },
                PaletteDark = new PaletteDark
                {
                    Primary = "#80CBC4",
                    Secondary = "#9FA8DA",
                    AppbarBackground = "#000000",
                    AppbarText = "#FFFFFF",
                    Background = "#000000",
                    Surface = "#121212",
                    TextPrimary = "#FFFFFF",
                    TextSecondary = "#FFFFFF",
                    TextDisabled = "#BDBDBD",
                    LinesDefault = "#FFFFFF",
                    Divider = "#FFFFFF",
                    ActionDefault = "#FFFFFF",
                    TableLines = "#FFFFFF",
                    TableStriped = "#1A1A1A",
                    TableHover = "#2A2A2A"
                },
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
                // Colores del logo de Alimentando Corazones: verde esmeralda,
                // naranja y verde lima.
                Primary = "#00A88E",
                Secondary = "#F58220",
                Tertiary = "#A6CE39",
                // Esmeralda un punto más profundo que Primary (mismo matiz y
                // saturación, luminosidad 26% en vez de 33%): con #00A88E el texto
                // blanco de la barra daba 3.00:1, por debajo del 4.5:1 que pide
                // WCAG AA para texto normal. Con este tono llega a 4.51:1.
                AppbarBackground = "#008672",
                AppbarText = "#FFFFFF",
                Background = "#F5F5F0",
                Surface = "#FFFFFF",
                TextPrimary = "#1A1A1A",
                TextSecondary = "#424242",
                DrawerBackground = "#FFFFFF",
                DrawerText = "#1A1A1A",
                DrawerIcon = "#00A88E"
            },
            PaletteDark = new PaletteDark
            {
                // Versiones más claras y suaves de los mismos colores: los tonos
                // vivos del logo resultan demasiado intensos sobre fondo oscuro.
                Primary = "#4DD0B1",
                Secondary = "#FFA35C",
                Tertiary = "#C5E17A",
                AppbarBackground = "#00695C",
                AppbarText = "#FFFFFF",
                Background = "#121212",
                Surface = "#1E1E1E",
                TextPrimary = "#F5F5F5",
                TextSecondary = "#BDBDBD",
                DrawerBackground = "#1E1E1E",
                DrawerText = "#F5F5F5",
                DrawerIcon = "#4DD0B1"
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "8px",
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
