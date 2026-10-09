using System.ComponentModel.DataAnnotations;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.DTOs.Inventario
{
    public class EntradaInventarioCrearDto
    {
        [Required(ErrorMessage = "El nombre del artículo es obligatorio.")]
        public string NombreArticulo { get; set; } = string.Empty;

        // Opcionales y solo aplican cuando el artículo es nuevo: si ya existe, se
        // usa el código y la ubicación que ya tiene guardados (ver RegistrarEntradaAsync).
        public string? Codigo { get; set; }
        public string? Ubicacion { get; set; }

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public string Categoria { get; set; } = string.Empty;

        // Solo aplica a Equipo (ver EstadosArticulo), donde es obligatorio y forma
        // parte de la identidad del artículo: junto con el nombre decide a QUÉ
        // artículo del catálogo suma la entrada. Sin [Required] aquí porque depende
        // de la categoría, que un atributo no puede ver; lo valida el servicio.
        public string? Estado { get; set; }

        [Required(ErrorMessage = "La unidad de medida es obligatoria.")]
        public string UnidadMedida { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Cantidad { get; set; }

        // Fecha queda sin [Required]: DateTime no-nullable siempre "tiene valor"
        // para DataAnnotations, así que la validación no dispararía nunca.
        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "El origen es obligatorio.")]
        public string Origen { get; set; } = string.Empty;

        public int? DonanteId { get; set; }
        public int? GastoOperativoId { get; set; }
        public string? Observaciones { get; set; }
    }

    public class SalidaDonacionCrearDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un artículo.")]
        public int ArticuloId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Cantidad { get; set; }

        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "La comunidad destinataria es obligatoria.")]
        public string ComunidadDestinataria { get; set; } = string.Empty;

        public string? Observaciones { get; set; }
    }

    public class ArticuloExistenciaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Codigo { get; set; }
        public string Categoria { get; set; } = string.Empty;

        // Null salvo en Equipo (ver EstadosArticulo).
        public string? Estado { get; set; }

        public string UnidadMedida { get; set; } = string.Empty;
        public string? Ubicacion { get; set; }
        public int StockActual { get; set; }
        public bool StockBajo { get; set; }

        // Nombre + estado, para donde el nombre solo es ambiguo (buscadores de
        // préstamo y entrega, donde dos sillas en distinto estado se confundirían).
        public string Etiqueta => Articulo.EtiquetaDe(Nombre, Estado);
    }

    public class FiltrosExistenciaDto
    {
        public const int TamanoPaginaPredeterminado = 20;

        // Techo duro: ningún llamador puede pedir una página tan grande que anule
        // la paginación y traiga el catálogo entero.
        public const int TamanoPaginaMaximo = 100;

        // Busca por Nombre o por Código en la misma caja: quien tiene el artículo
        // en la mano puede escribir cualquiera de los dos.
        public string? Nombre { get; set; }
        public string? Categoria { get; set; }

        // Igualdad exacta, igual que Categoria: se elige de la lista de estados.
        // Cuenta como filtro por sí solo (ver HayFiltro en ExistenciasInventario).
        public string? Estado { get; set; }

        // Cuenta como filtro por sí solo, igual que Nombre y Categoria: activarlo
        // sin ningún otro criterio tiene que mostrar igual los artículos con poco
        // stock, que es exactamente el caso de uso (entrar a reponer, no a buscar
        // un artículo puntual). Cuando está activo, el repositorio además ordena
        // de menor a mayor stock en vez de por nombre: lo más urgente primero.
        public bool SoloStockBajo { get; set; }

        // Base 0, igual que el índice de página de la grilla. El repositorio la
        // resuelve en SQL con Skip/Take: nunca se traen los registros anteriores.
        public int Pagina { get; set; }
        public int TamanoPagina { get; set; } = TamanoPaginaPredeterminado;

        // Valores saneados: el repositorio usa estos, no los crudos, para que una
        // página negativa o un tamaño de 0 no rompan el Skip/Take.
        public int PaginaEfectiva => Pagina < 0 ? 0 : Pagina;

        public int TamanoPaginaEfectivo => Math.Clamp(
            TamanoPagina <= 0 ? TamanoPaginaPredeterminado : TamanoPagina,
            1,
            TamanoPaginaMaximo);
    }

    public class ArticuloEditarDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        // Opcional: no todo artículo tiene un código asignado.
        public string? Codigo { get; set; }

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public string Categoria { get; set; } = string.Empty;

        // Obligatorio solo si la categoría es Equipo; lo valida el servicio.
        public string? Estado { get; set; }

        [Required(ErrorMessage = "La unidad de medida es obligatoria.")]
        public string UnidadMedida { get; set; } = string.Empty;

        public string? Ubicacion { get; set; }

        // Antes fijo en 5 para todo artículo (el valor por defecto de la entidad);
        // ahora se puede ajustar por artículo desde acá.
        [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")]
        public int StockMinimo { get; set; }

        // Solo informativo: se muestra de solo lectura, no se edita desde acá.
        public int StockActual { get; set; }
    }

    public class MovimientoInventarioDto
    {
        public int Id { get; set; }
        public string Articulo { get; set; } = string.Empty;

        // "Entrada", "Donacion" o "Prestamo" (estas dos últimas son los valores de
        // SalidaInventario.TipoSalida): distingue el sub-tipo de salida en vez de
        // agrupar todo bajo "Salida", para que el historial pueda filtrar por
        // cada uno por separado.
        public string TipoMovimiento { get; set; } = string.Empty;

        public int Cantidad { get; set; }
        public DateTime Fecha { get; set; }
        public string? OrigenODestino { get; set; }
    }

    public class FiltrosMovimientoDto
    {
        public const string TipoEntrada = "Entrada";

        public const int TamanoPaginaPredeterminado = 20;

        // Techo duro: ningún llamador puede pedir una página tan grande que anule
        // la paginación y traiga el historial entero.
        public const int TamanoPaginaMaximo = 100;

        public int? ArticuloId { get; set; }
        public string? TipoMovimiento { get; set; } // "Entrada", "Donacion", "Prestamo" o null (todos)
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }

        // Solo los lee el historial paginado (ObtenerPaginaHistorialMovimientosAsync);
        // el completo, que usan los reportes para exportar, los ignora.
        // Base 0, igual que el índice de página de la grilla.
        public int Pagina { get; set; }
        public int TamanoPagina { get; set; } = TamanoPaginaPredeterminado;

        // Valores saneados: el repositorio usa estos, no los crudos, para que una
        // página negativa o un tamaño de 0 no rompan el Skip/Take.
        public int PaginaEfectiva => Pagina < 0 ? 0 : Pagina;

        public int TamanoPaginaEfectivo => Math.Clamp(
            TamanoPagina <= 0 ? TamanoPaginaPredeterminado : TamanoPagina,
            1,
            TamanoPaginaMaximo);

        // TipoMovimiento no es una columna: decide CUÁLES de las dos tablas se
        // consultan y, con "Donacion" o "Prestamo", qué salidas. Una sola fuente de
        // esa regla para el servicio y el repositorio. Un valor desconocido no
        // incluye ninguna: no devuelve nada, en vez de fallar.
        public bool IncluyeEntradas => TipoMovimiento is null or TipoEntrada;

        public bool IncluyeSalidas => TipoMovimiento is null
            or Domain.TiposSalidaInventario.Donacion
            or Domain.TiposSalidaInventario.Prestamo;

        // Null cuando el filtro no acota el tipo de salida (todas las salidas).
        public string? TipoSalida => TipoMovimiento is Domain.TiposSalidaInventario.Donacion or Domain.TiposSalidaInventario.Prestamo
            ? TipoMovimiento
            : null;
    }

    public class HistorialMovimientosResultadoDto
    {
        // En el historial paginado, solo los de la página pedida.
        public List<MovimientoInventarioDto> Movimientos { get; set; } = new();

        // Cuántos movimientos cumplen el filtro en total (no los de la página): lo
        // necesita el paginador para saber cuántas páginas hay sin traerlas.
        public int TotalRegistros { get; set; }

        // Unidades (no cantidad de movimientos) de TODO el período filtrado.
        public int TotalEntradas { get; set; }
        public int TotalSalidas { get; set; }
    }

    public class SolicitudPrestamoCrearDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un artículo.")]
        public int ArticuloId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Cantidad { get; set; }

        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "La actividad es obligatoria.")]
        public string Actividad { get; set; } = string.Empty;

        [Required(ErrorMessage = "El solicitante es obligatorio.")]
        public string Solicitante { get; set; } = string.Empty;
    }

    public class ResolucionPrestamoDto
    {
        public int SolicitudId { get; set; }
        public bool Aprobado { get; set; }
        public string? MotivoRechazo { get; set; }
    }

    public class SolicitudPrestamoListaDto
    {
        public int Id { get; set; }
        public string Articulo { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public DateTime Fecha { get; set; }
        public string Actividad { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;

        // Complemento natural de Estado: dice CUÁNDO se resolvió. Null mientras la
        // solicitud sigue Pendiente.
        public DateTime? FechaResolucion { get; set; }
    }
}