using System.ComponentModel.DataAnnotations;
using SIGAC.Domain;

namespace SIGAC.Application.DTOs.Gastos
{
    // Lo que captura el formulario de un gasto, común a registrar y editar. Lo
    // implementan los dos DTOs para que CamposGasto.razor y
    // GastoOperativoValidator.Validar trabajen sobre una sola forma y las dos
    // pantallas no puedan divergir.
    public interface IDatosGastoOperativo
    {
        int TipoGastoId { get; set; }
        string Proveedor { get; set; }
        string NumeroFactura { get; set; }
        DateTime Fecha { get; set; }
        decimal MontoSinIva { get; set; }
        decimal Iva { get; set; }
        string Moneda { get; set; }
        string FormaPago { get; set; }
        string? NumeroCheque { get; set; }
        string CuentaContable { get; set; }
        string DescripcionCuenta { get; set; }
        string Descripcion { get; set; }
        string Responsable { get; set; }
    }

    // Solo lo que captura el formulario: Id, Estado y FechaRegistro los pone el
    // servicio al crear la entidad. Moneda, FormaPago y las dos columnas de
    // cuenta nacen con los valores que traen todas las líneas del reporte de la
    // contadora, para que el formulario ya aparezca precargado.
    public class GastoOperativoCrearDto : IDatosGastoOperativo
    {
        [Range(1, int.MaxValue, ErrorMessage = "El tipo de gasto es obligatorio.")]
        public int TipoGastoId { get; set; }

        [Required(ErrorMessage = "El proveedor es obligatorio.")]
        public string Proveedor { get; set; } = string.Empty;

        [Required(ErrorMessage = "El número de factura es obligatorio.")]
        public string NumeroFactura { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto sin IVA debe ser mayor a 0.")]
        public decimal MontoSinIva { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El IVA no puede ser negativo.")]
        public decimal Iva { get; set; }

        [Required(ErrorMessage = "La moneda es obligatoria.")]
        public string Moneda { get; set; } = TiposMoneda.Colones;

        [Required(ErrorMessage = "La forma de pago es obligatoria.")]
        public string FormaPago { get; set; } = FormasPago.Contado;

        public string? NumeroCheque { get; set; }

        [Required(ErrorMessage = "La cuenta contable es obligatoria.")]
        public string CuentaContable { get; set; } = ReglasGastoOperativo.CuentaContablePorDefecto;

        [Required(ErrorMessage = "La descripción de cuenta es obligatoria.")]
        public string DescripcionCuenta { get; set; } = ReglasGastoOperativo.DescripcionCuentaPorDefecto;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        public string Descripcion { get; set; } = string.Empty;

        [Required(ErrorMessage = "El responsable es obligatorio.")]
        public string Responsable { get; set; } = string.Empty;
    }

    // Mismos campos que el de alta; los valores llegan siempre de la base
    // (ObtenerParaEditarAsync), así que no lleva valores iniciales.
    public class GastoOperativoEditarDto : IDatosGastoOperativo
    {
        [Range(1, int.MaxValue, ErrorMessage = "El tipo de gasto es obligatorio.")]
        public int TipoGastoId { get; set; }

        [Required(ErrorMessage = "El proveedor es obligatorio.")]
        public string Proveedor { get; set; } = string.Empty;

        [Required(ErrorMessage = "El número de factura es obligatorio.")]
        public string NumeroFactura { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto sin IVA debe ser mayor a 0.")]
        public decimal MontoSinIva { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El IVA no puede ser negativo.")]
        public decimal Iva { get; set; }

        [Required(ErrorMessage = "La moneda es obligatoria.")]
        public string Moneda { get; set; } = string.Empty;

        [Required(ErrorMessage = "La forma de pago es obligatoria.")]
        public string FormaPago { get; set; } = string.Empty;

        public string? NumeroCheque { get; set; }

        [Required(ErrorMessage = "La cuenta contable es obligatoria.")]
        public string CuentaContable { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción de cuenta es obligatoria.")]
        public string DescripcionCuenta { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        public string Descripcion { get; set; } = string.Empty;

        [Required(ErrorMessage = "El responsable es obligatorio.")]
        public string Responsable { get; set; } = string.Empty;
    }

    public class GastoOperativoListaDto
    {
        public int Id { get; set; }
        public int TipoGastoId { get; set; }

        // Nombre listo para mostrar, tal como está en TiposGasto: la pantalla no
        // traduce nada.
        public string TipoGasto { get; set; } = string.Empty;

        public string Proveedor { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public decimal MontoSinIva { get; set; }
        public decimal Iva { get; set; }

        // Lo que efectivamente salió de la cuenta. Se calcula acá y no en cada
        // pantalla, para que el listado, los totales y el selector de Inventario
        // muestren la misma cifra.
        public decimal Total => MontoSinIva + Iva;

        public string Moneda { get; set; } = string.Empty;
        public string FormaPago { get; set; } = string.Empty;
        public string? NumeroCheque { get; set; }
        public string CuentaContable { get; set; } = string.Empty;
        public string DescripcionCuenta { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }

    public class FiltrosGastoDto
    {
        public const int TamanoPaginaPredeterminado = 20;

        // Techo duro: ningún llamador puede pedir una página tan grande que anule
        // la paginación y traiga todos los gastos.
        public const int TamanoPaginaMaximo = 100;

        // Busca en el proveedor (sin distinguir tildes) o en el número de factura:
        // son los dos datos que la persona tiene a mano con el papel delante.
        public string? Texto { get; set; }

        public int? TipoGastoId { get; set; }

        // true: solo gastos cuyo tipo ingresa al inventario (TipoGasto.GeneraInventario).
        // Lo usa el selector de RegistrarEntradaInventario; null no filtra.
        public bool? GeneraInventario { get; set; }

        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        // Solo los lee el listado paginado (ObtenerPaginaGastosAsync); la consulta
        // completa, que usa el selector de gastos de Inventario, los ignora.
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
    }

    // El total es del conjunto filtrado completo, no de lo que se ve en
    // pantalla: por eso viaja junto con la lista en vez de calcularse en el
    // frontend, que solo ve una página.
    //
    // Un total POR MONEDA y no un solo decimal: desde que el gasto declara en
    // qué moneda se pagó, sumar colones con dólares en un único número dejaría
    // de representar nada.
    //
    // TotalRegistros es cuántos gastos cumplen el filtro en total (no los de la
    // página): lo necesita el paginador para saber cuántas páginas hay.
    public sealed record GastosConsultaDto(
        IReadOnlyList<GastoOperativoListaDto> Gastos,
        IReadOnlyList<MontoPorMonedaDto> TotalesPorMoneda,
        int TotalRegistros);

    public class AnulacionGastoDto
    {
        public int GastoId { get; set; }

        [Required(ErrorMessage = "El motivo de anulación es obligatorio.")]
        public string MotivoAnulacion { get; set; } = string.Empty;
    }

    public class TipoGastoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public bool GeneraInventario { get; set; }
        public string? CuentaContablePorDefecto { get; set; }
    }

    // Alta y edición de un tipo usan los mismos campos; Activo se mueve por su
    // propio camino (ActivarAsync / DesactivarAsync), igual que en Donantes.
    public class TipoGastoGuardarDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        public bool GeneraInventario { get; set; }

        public string? CuentaContablePorDefecto { get; set; } = ReglasGastoOperativo.CuentaContablePorDefecto;
    }
}
