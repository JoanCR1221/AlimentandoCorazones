using System.ComponentModel.DataAnnotations;
using SIGAC.Application.Validators;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.DTOs.Alquileres
{
    // ----------------------------------------------------------------------
    // Arrendatarios
    // ----------------------------------------------------------------------

    // Las longitudes salen de ArrendatarioValidator, que es la barrera del servidor
    // y espeja las columnas de SigacDbContext: el formulario y la base no pueden
    // quedar con topes distintos.
    public class ArrendatarioCrearDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(ArrendatarioValidator.LongitudMaximaNombre, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo de persona es obligatorio.")]
        public string TipoPersona { get; set; } = string.Empty;

        // Opcional; si viene, no puede repetirse (UX_Arrendatarios_Identificacion).
        [StringLength(ArrendatarioValidator.LongitudMaximaIdentificacion, ErrorMessage = "La identificación no puede superar los {1} caracteres.")]
        public string? Identificacion { get; set; }

        // Código de país sin "+" ("506") y número local; ver ReglasTelefono.
        public string? CodigoPaisTelefono { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        public string? Telefono { get; set; }

        [StringLength(ArrendatarioValidator.LongitudMaximaCorreo, ErrorMessage = "El correo no puede superar los {1} caracteres.")]
        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        public string? Correo { get; set; }
    }

    // Solo lectura: lo que muestra el buscador de arrendatarios al registrar un
    // alquiler. Sin DataAnnotations, igual que DonanteListaDto.
    public class ArrendatarioListaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string TipoPersona { get; set; } = string.Empty;
        public string? Identificacion { get; set; }
        public string? TelefonoCompleto { get; set; }
        public string? Correo { get; set; }

        // Texto del buscador: con la identificación, cuando la hay, para distinguir
        // a dos homónimos.
        public string Etiqueta => string.IsNullOrWhiteSpace(Identificacion) ? Nombre : $"{Nombre} ({Identificacion})";
    }

    // ----------------------------------------------------------------------
    // Espacios y características (catálogos)
    // ----------------------------------------------------------------------

    public class EspacioFisicoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int? Capacidad { get; set; }
        public bool Estado { get; set; }

        public string Etiqueta => Capacidad.HasValue ? $"{Nombre} ({Capacidad} personas)" : Nombre;
    }

    public class EspacioFisicoCrearDto
    {
        [Required(ErrorMessage = "El nombre del espacio es obligatorio.")]
        [StringLength(EspaciosValidator.LongitudMaximaNombre, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        // Opcional: ver EspacioFisico.Capacidad.
        [Range(1, EspaciosValidator.CapacidadMaxima, ErrorMessage = "La capacidad debe estar entre {1} y {2} personas.")]
        public int? Capacidad { get; set; }
    }

    // Horario de alquiler para leer y editar desde "Espacios y características".
    // Nullables porque MudTimePicker arranca vacío; AlquilerValidator.ValidarHorario
    // exige las cuatro.
    public class HorarioAlquilerDto
    {
        public TimeSpan? AperturaEntreSemana { get; set; }
        public TimeSpan? CierreEntreSemana { get; set; }
        public TimeSpan? AperturaFinDeSemana { get; set; }
        public TimeSpan? CierreFinDeSemana { get; set; }

        // Para aplicar ReglasAlquiler en la pantalla de registro. Solo tiene sentido
        // con las cuatro horas cargadas, que es como lo devuelve
        // IEspaciosService.ObtenerHorarioAsync.
        public HorarioAlquiler AHorario() => new()
        {
            AperturaEntreSemana = AperturaEntreSemana ?? TimeSpan.Zero,
            CierreEntreSemana = CierreEntreSemana ?? TimeSpan.Zero,
            AperturaFinDeSemana = AperturaFinDeSemana ?? TimeSpan.Zero,
            CierreFinDeSemana = CierreFinDeSemana ?? TimeSpan.Zero
        };
    }

    public class CaracteristicaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Estado { get; set; }
    }

    public class CaracteristicaCrearDto
    {
        [Required(ErrorMessage = "El nombre de la característica es obligatorio.")]
        [StringLength(EspaciosValidator.LongitudMaximaNombre, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
        public string Nombre { get; set; } = string.Empty;
    }

    // ----------------------------------------------------------------------
    // Alquileres
    // ----------------------------------------------------------------------

    // Sin EsFinDeSemana ni HorarioFinSemana, que traía la propuesta: salen de la
    // fecha (ReglasAlquiler). SectorAsignado pasó a EspacioIds porque un alquiler
    // usa uno o varios sectores.
    //
    // IValidatableObject para que el formulario avise de una fecha pasada o de
    // horas mal cargadas antes de viajar al servidor. El texto sale de
    // AlquilerValidator, que es el mismo que valida en el servicio. Que las horas
    // caigan dentro del horario configurado no se valida acá (el DTO no conoce ese
    // horario): la pantalla lo avisa aparte y el servicio lo exige al guardar.
    public class AlquilerCrearDto : IValidatableObject
    {
        // [Range] y no [Required]: un int sin elegir llega como 0 y [Required] lo
        // daría por válido. Mismo recurso que DonacionEntregadaCrearDto.ArticuloId.
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un arrendatario.")]
        public int ArrendatarioId { get; set; }

        public DateTime Fecha { get; set; }

        // Nullable porque MudTimePicker arranca vacío: sin esto "sin elegir" sería
        // 00:00 y pasaría por una hora válida.
        public TimeSpan? HoraInicio { get; set; }
        public TimeSpan? HoraFin { get; set; }

        [MinLength(1, ErrorMessage = "Debe elegir al menos un sector.")]
        public List<int> EspacioIds { get; set; } = new();

        // Puede ir vacía: un alquiler no está obligado a pedir servicios extra.
        public List<int> CaracteristicaIds { get; set; } = new();

        [Range(1, AlquilerValidator.CantidadPersonasMaxima, ErrorMessage = "La cantidad de personas debe estar entre {1} y {2}.")]
        public int CantidadPersonas { get; set; }

        // Cota inferior 0 y no 0,01 como en DonacionDineroCrearDto: un préstamo sin
        // costo también se registra.
        [Range(0, double.MaxValue, ErrorMessage = "El monto no puede ser negativo.")]
        public decimal Monto { get; set; }

        [Required(ErrorMessage = "La moneda es obligatoria.")]
        public string Moneda { get; set; } = string.Empty;

        [StringLength(AlquilerValidator.LongitudMaximaObservaciones, ErrorMessage = "Las observaciones no pueden superar los {1} caracteres.")]
        public string? Observaciones { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var error = AlquilerValidator.DescribirErrorDeFechaYHoras(Fecha, HoraInicio, HoraFin);

            if (error is not null)
                yield return new ValidationResult(error);
        }
    }

    // Una fila del calendario. Arrendatario, sectores y características ya vienen
    // resueltos a texto: la grilla no tiene que ir a buscarlos.
    public class AlquilerListaDto
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public string Arrendatario { get; set; } = string.Empty;
        public string? TelefonoArrendatario { get; set; }
        public List<string> Espacios { get; set; } = new();
        public List<string> Caracteristicas { get; set; } = new();
        public int CantidadPersonas { get; set; }
        public decimal Monto { get; set; }
        public string Moneda { get; set; } = string.Empty;
        public EstadoAlquiler Estado { get; set; }
        public string? MotivoCancelacion { get; set; }
        public string? Observaciones { get; set; }

        // Derivado, no guardado: ver ReglasAlquiler.
        public bool EsFinDeSemana => ReglasAlquiler.EsFinDeSemana(Fecha);
    }

    public class FiltrosHistorialAlquilerDto
    {
        public const int TamanoPaginaPredeterminado = 20;

        // Techo duro: ningún llamador puede pedir una página tan grande que anule
        // la paginación y traiga el calendario entero.
        public const int TamanoPaginaMaximo = 100;

        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        // El "Sector" de la propuesta: alquileres que usan este espacio, entre otros.
        public int? EspacioId { get; set; }

        // Null trae reservados y cancelados.
        public EstadoAlquiler? Estado { get; set; }

        // Solo los lee el calendario paginado (ObtenerPaginaHistorialAlquileresAsync);
        // el historial completo, que usan el reporte y la ocupación del día, los
        // ignora. Base 0, igual que el índice de página de la grilla.
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

    // Las filas más el ingreso del período, mismo patrón que
    // HistorialDonacionesResultadoDto: el total se calcula sobre todo lo que cumple
    // el filtro y por moneda, porque sumar colones con dólares no representa nada.
    public class HistorialAlquileresResultadoDto
    {
        // En el calendario paginado, solo los de la página pedida.
        public List<AlquilerListaDto> Alquileres { get; set; } = new();

        // Cuántos alquileres cumplen el filtro en total (no los de la página): lo
        // necesita el paginador para saber cuántas páginas hay sin traerlas.
        public int TotalRegistros { get; set; }

        // Solo alquileres reservados: uno cancelado no genera ingreso.
        public List<MontoPorMonedaDto> TotalesPorMoneda { get; set; } = new();
    }

    public class CancelacionAlquilerDto
    {
        public int AlquilerId { get; set; }
        public string? MotivoCancelacion { get; set; }
    }
}
