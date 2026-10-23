using Microsoft.Extensions.Localization;
using MudBlazor;

namespace SIGAC.Services;

// Textos propios de los componentes de MudBlazor (paginador, menús de la grilla,
// calendario) en español. MudBlazor los trae solo en inglés y el resto de SIGAC está en
// español, así que el paginador decía «Rows per page». Una clave que no está acá se
// resuelve con el interceptor por defecto (el texto en inglés de MudBlazor), no con la
// clave a secas.
internal sealed class InterceptorTraduccionMudBlazor : ILocalizationInterceptor
{
    private readonly DefaultLocalizationInterceptor _porDefecto;

    public InterceptorTraduccionMudBlazor(ILoggerFactory loggerFactory)
    {
        _porDefecto = new DefaultLocalizationInterceptor(loggerFactory, null!);
    }

    // Los textos con datos van con marcadores POSICIONALES ({0}, {1}...): MudBlazor 9.x
    // le pasa a este interceptor los valores como texto y en orden, sin nombres, y se
    // pasan por string.Format. Con marcadores con nombre ({first_item}...) lanza
    // FormatException al pintar el paginador y tumba el circuito de la pantalla.
    private static readonly Dictionary<string, string> Textos = new()
    {
        ["MudDataGridPager_RowsPerPage"] = "Filas por página:",
        // Orden de los valores: primer registro, último registro, total.
        ["MudDataGridPager_InfoFormat"] = "{0}-{1} de {2}",
        ["MudDataGridPager_AllItems"] = "Todas",
        ["MudDataGridPager_FirstPage"] = "Primera página",
        ["MudDataGridPager_PreviousPage"] = "Página anterior",
        ["MudDataGridPager_NextPage"] = "Página siguiente",
        ["MudDataGridPager_LastPage"] = "Última página",

        ["MudTablePager_FirstPage"] = "Primera página",
        ["MudTablePager_PreviousPage"] = "Página anterior",
        ["MudTablePager_NextPage"] = "Página siguiente",
        ["MudTablePager_LastPage"] = "Última página",

        ["MudDataGrid_Loading"] = "Cargando...",
        ["MudDataGrid_RefreshData"] = "Actualizar",
        ["MudDataGrid_Filter"] = "Filtrar",
        ["MudDataGrid_Sort"] = "Ordenar",
        ["MudDataGrid_Unsort"] = "Quitar orden",
        ["MudDataGrid_Apply"] = "Aplicar",
        ["MudDataGrid_Cancel"] = "Cancelar",
        ["MudDataGrid_Clear"] = "Limpiar",
        ["MudDataGrid_ClearFilter"] = "Limpiar filtro",
        ["MudDataGrid_Save"] = "Guardar",
        ["MudDataGrid_Columns"] = "Columnas",
        ["MudDataGrid_Hide"] = "Ocultar",
        ["MudDataGrid_ShowAll"] = "Mostrar todas",
        ["MudDataGrid_HideAll"] = "Ocultar todas",
        ["MudDataGrid_SelectAllRows"] = "Seleccionar todas las filas",
        ["MudDataGrid_SelectRow"] = "Seleccionar fila",

        ["MudDialog_Close"] = "Cerrar",

        ["MudBaseDatePicker_NextMonth"] = "Mes siguiente",
        ["MudBaseDatePicker_PrevMonth"] = "Mes anterior",
        ["MudBaseDatePicker_NextYear"] = "Año siguiente",
        ["MudBaseDatePicker_PrevYear"] = "Año anterior",
        ["MudBaseDatePicker_Open"] = "Abrir calendario",
        ["MudDateRangePicker_Start"] = "Inicio",
        ["MudDateRangePicker_End"] = "Fin"
    };

    public LocalizedString Handle(string key, params object[] arguments)
    {
        if (!Textos.TryGetValue(key, out var texto))
            return _porDefecto.Handle(key, arguments);

        return new LocalizedString(key, arguments is { Length: > 0 } ? string.Format(texto, arguments) : texto);
    }
}
