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
            // Misma paleta artesanal de SIGAC (terracota/marrón), pero llevada
            // al extremo de contraste que este modo exige: fondo/texto casi
            // blanco puro / negro puro (no el crema #FDFBF7 ni el marrón
            // #3E332E de los temas normales, que rinden menos contraste) y el
            // terracota oscurecido a un tono "oxblood" para que el texto
            // blanco del AppBar llegue a AAA (7:1), no solo AA (4.5:1) como
            // en el tema claro normal. Radios rectos y sin animaciones se
            // mantienen intactos (ver app.css / MainLayout.razor.css, todo
            // guardado con :not([data-alto-contraste="true"])).
            return new MudTheme
            {
                PaletteLight = new PaletteLight
                {
                    // #6B3226 con texto blanco da 9.95:1 (el #A8503F del tema
                    // normal solo llega a 5.57:1: alcanza para AA, no para el
                    // AAA que busca este modo).
                    Primary = "#6B3226",
                    Secondary = "#3E332E",
                    AppbarBackground = "#6B3226",
                    AppbarText = "#FFFFFF",
                    Background = "#FFFCF8",
                    Surface = "#FFFFFF",
                    TextPrimary = "#000000",
                    TextSecondary = "#000000",
                    TextDisabled = "#424242",
                    LinesDefault = "#000000",
                    Divider = "#000000",
                    ActionDefault = "#000000",
                    TableLines = "#000000",
                    TableStriped = "#F5EDE4",
                    TableHover = "#EDE0D2"
                },
                PaletteDark = new PaletteDark
                {
                    Primary = "#FFAB91",
                    Secondary = "#D7CCC0",
                    AppbarBackground = "#0D0906",
                    AppbarText = "#FFFFFF",
                    Background = "#0D0906",
                    Surface = "#161009",
                    TextPrimary = "#FFFFFF",
                    TextSecondary = "#FFFFFF",
                    TextDisabled = "#C4B8AC",
                    LinesDefault = "#FFFFFF",
                    Divider = "#FFFFFF",
                    ActionDefault = "#FFFFFF",
                    TableLines = "#FFFFFF",
                    TableStriped = "#1F1710",
                    TableHover = "#2C2018"
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
                // Paleta artesanal (hand-drawn) de Alimentando Corazones:
                // terracota, beige cálido y papel crema.
                // #A8503F en vez del #C05C4E pedido para Primary/AppbarBackground:
                // con texto blanco encima (AppBar, botón principal) #C05C4E da
                // 4.30:1, por debajo del 4.5:1 que pide WCAG AA para texto normal.
                // Con este tono llega a 5.57:1. El #C05C4E literal se conserva
                // para el borde/texto de los botones secundarios (ver app.css,
                // .mud-button-outlined-primary), donde el texto no es blanco y
                // no hay problema de contraste.
                Primary = "#A8503F",
                Secondary = "#7A685D",
                AppbarBackground = "#A8503F",
                AppbarText = "#FFFFFF",
                Background = "#E6D7C3",
                Surface = "#FDFBF7",
                TextPrimary = "#3E332E",
                TextSecondary = "#6B5C52",
                DrawerBackground = "#FDFBF7",
                DrawerText = "#3E332E",
                DrawerIcon = "#A8503F",
                LinesDefault = "#7A685D",
                LinesInputs = "#7A685D",
                Divider = "#7A685D",
                TableLines = "#7A685D"
            },
            PaletteDark = new PaletteDark
            {
                // Misma paleta artesanal en tonos oscuros. La imagen de
                // referencia solo mostraba el tema claro; este modo oscuro
                // extiende la misma estética en vez de dejarlo desactualizado
                // con los colores del logo anterior. #B85A45 da 4.58:1 con
                // texto blanco, igual criterio de contraste que en claro.
                Primary = "#B85A45",
                Secondary = "#A99C8E",
                AppbarBackground = "#B85A45",
                AppbarText = "#FFFFFF",
                Background = "#2A2420",
                Surface = "#362F29",
                TextPrimary = "#F0E6DA",
                TextSecondary = "#C9BBAE",
                DrawerBackground = "#362F29",
                DrawerText = "#F0E6DA",
                DrawerIcon = "#B85A45",
                LinesDefault = "#5B4F45",
                LinesInputs = "#5B4F45",
                Divider = "#5B4F45",
                TableLines = "#5B4F45"
            },
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
