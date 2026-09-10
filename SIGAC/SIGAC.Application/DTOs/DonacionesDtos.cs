using System.ComponentModel.DataAnnotations;

namespace SIGAC.Application.DTOs.Donaciones
{
    // Longitudes máximas de los campos de texto. Están acá y no como literales
    // sueltos en cada [StringLength] para que el DTO y la columna no puedan
    // divergir: cada valor es exactamente el HasMaxLength configurado en
    // SigacDbContext. Si una migración cambia una columna, se cambia acá y todos
    // los DTOs que la usan quedan alineados.
    //
    // Los DTOs de Inventario y Beneficiarios todavía no declaran longitudes: dejan
    // que la validación la haga la base al guardar, y un texto demasiado largo se
    // convierte en una excepción de SQL en vez de un mensaje en el formulario. Acá
    // se valida antes, que es lo que corresponde.
    internal static class LongitudesDonaciones
    {
        public const int Nombre = 150;
        public const int TipoPersona = 20;
        public const int Telefono = 20;
        public const int Correo = 150;
        public const int NombreArticulo = 150;
        public const int Categoria = 100;
        public const int UnidadMedida = 50;
        public const int TipoDestinatario = 20;
        public const int ComunidadDestinataria = 150;
        public const int Observaciones = 500;
    }

    // ----------------------------------------------------------------------
    // Donantes
    // ----------------------------------------------------------------------

    public class DonanteCrearDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(LongitudesDonaciones.Nombre, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo de persona es obligatorio.")]
        [StringLength(LongitudesDonaciones.TipoPersona)]
        public string TipoPersona { get; set; } = string.Empty;

        // Opcionales: se recibe la donación igual aunque el donante no deje datos
        // de contacto.
        [StringLength(LongitudesDonaciones.Telefono, ErrorMessage = "El teléfono no puede superar los {1} caracteres.")]
        public string? Telefono { get; set; }

        // [EmailAddress] además de la longitud: es el único campo del módulo con un
        // formato reconocible, y un correo mal escrito no se detecta después.
        [StringLength(LongitudesDonaciones.Correo, ErrorMessage = "El correo no puede superar los {1} caracteres.")]
        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        public string? Correo { get; set; }
    }

    // Mismos campos que DonanteCrearDto, en un tipo aparte y no reutilizando aquel:
    // es el mismo criterio que separa BeneficiarioCrearDto de BeneficiarioEditarDto.
    // Los dos formularios evolucionan por separado (al editar puede aparecer el
    // Estado, al crear no), y compartir el tipo obliga a que un cambio en uno
    // arrastre al otro.
    public class DonanteEditarDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(LongitudesDonaciones.Nombre, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo de persona es obligatorio.")]
        [StringLength(LongitudesDonaciones.TipoPersona)]
        public string TipoPersona { get; set; } = string.Empty;

        [StringLength(LongitudesDonaciones.Telefono, ErrorMessage = "El teléfono no puede superar los {1} caracteres.")]
        public string? Telefono { get; set; }

        [StringLength(LongitudesDonaciones.Correo, ErrorMessage = "El correo no puede superar los {1} caracteres.")]
        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        public string? Correo { get; set; }
    }

    // Solo lectura: lo que muestra la grilla. Sin DataAnnotations, igual que
    // ArticuloExistenciaDto y BeneficiarioListaDto (nadie lo valida, sale de la BD).
    public class DonanteListaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string TipoPersona { get; set; } = string.Empty;
        public string? Correo { get; set; }
        public string? Telefono { get; set; }
        public bool Estado { get; set; }
    }

    public class FiltrosDonanteDto
    {
        public const int TamanoPaginaPredeterminado = 20;

        // Techo duro: ningún llamador puede pedir una página tan grande que anule
        // la paginación y traiga la tabla entera.
        public const int TamanoPaginaMaximo = 100;

        // Texto libre sobre el nombre. Se apoya en IX_Donantes_Nombre, que existe
        // justamente para que este filtro no recorra la tabla entera.
        public string? Nombre { get; set; }
        public string? TipoPersona { get; set; }

        // Nullable con tres estados, igual que en FiltrosBeneficiarioDto: true solo
        // activos, false solo inactivos, null todos. Un bool simple no podría
        // expresar "sin filtrar".
        public bool? Estado { get; set; }

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

    // ----------------------------------------------------------------------
    // Donaciones recibidas: dinero
    // ----------------------------------------------------------------------

    // Datos de un donante que se registra SIN salir del formulario de donación.
    // Existe para no obligar a interrumpir la carga: llega alguien a donar, no está
    // en la lista, y sin esto habría que abandonar el formulario, ir a Donantes,
    // registrarlo y volver a empezar.
    //
    // Es un tipo propio y no un DonanteCrearDto reutilizado porque se valida en un
    // contexto distinto: acá es opcional en bloque (puede venir null entero), y un
    // [Required] sobre DonanteCrearDto.Nombre dispararía aunque el usuario haya
    // elegido un donante existente.
    public class NuevoDonanteDto
    {
        [Required(ErrorMessage = "El nombre del donante es obligatorio.")]
        [StringLength(LongitudesDonaciones.Nombre, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo de persona es obligatorio.")]
        [StringLength(LongitudesDonaciones.TipoPersona)]
        public string TipoPersona { get; set; } = string.Empty;

        [StringLength(LongitudesDonaciones.Telefono, ErrorMessage = "El teléfono no puede superar los {1} caracteres.")]
        public string? Telefono { get; set; }

        [StringLength(LongitudesDonaciones.Correo, ErrorMessage = "El correo no puede superar los {1} caracteres.")]
        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        public string? Correo { get; set; }
    }

    public class DonacionDineroCrearDto
    {
        // DonanteId y NuevoDonante son las dos formas de indicar el mismo dato:
        // se elige un donante YA registrado (DonanteId) o se crea uno en el momento
        // (NuevoDonante). Por eso los dos son opcionales a nivel de tipo: cuál de
        // los dos viene lleno depende de lo que haya elegido el usuario, y marcar
        // cualquiera como obligatorio rompería la otra mitad de los casos.
        //
        // Que venga EXACTAMENTE uno de los dos (ni ninguno, ni ambos) no se puede
        // expresar con DataAnnotations sobre una sola propiedad: lo valida el
        // servicio, igual que la exclusión mutua del destinatario en las entregas.
        // La entidad DonacionDinero sí tiene DonanteId obligatorio; para cuando se
        // construye, el servicio ya resolvió cuál de los dos caminos se tomó.
        public int? DonanteId { get; set; }
        public NuevoDonanteDto? NuevoDonante { get; set; }

        // Cota inferior 0,01 y no 1: es el mínimo representable en decimal(18,2) y
        // respalda el CK_DonacionesDinero_Monto de la base. Los límites de [Range]
        // son double porque un atributo no admite constantes decimal; para una cota
        // de céntimos la conversión no pierde nada relevante.
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
        public decimal Monto { get; set; }

        // Fecha queda sin [Required]: DateTime no-nullable siempre "tiene valor"
        // para DataAnnotations, así que la validación no dispararía nunca.
        public DateTime Fecha { get; set; }

        [StringLength(LongitudesDonaciones.Observaciones, ErrorMessage = "Las observaciones no pueden superar los {1} caracteres.")]
        public string? Observaciones { get; set; }
    }

    // ----------------------------------------------------------------------
    // Donaciones recibidas: especie
    // ----------------------------------------------------------------------

    // Una línea del detalle de la donación. Nombre libre y no un ArticuloId, igual
    // que en la entidad DetalleDonacionEspecie: lo donado puede no existir todavía
    // en el catálogo, y obligar a elegirlo de una lista impediría registrar la
    // donación en el momento de recibirla.
    public class DetalleArticuloDto
    {
        [Required(ErrorMessage = "El nombre del artículo es obligatorio.")]
        [StringLength(LongitudesDonaciones.NombreArticulo, ErrorMessage = "El nombre del artículo no puede superar los {1} caracteres.")]
        public string NombreArticulo { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Cantidad { get; set; }

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        [StringLength(LongitudesDonaciones.Categoria)]
        public string Categoria { get; set; } = string.Empty;

        // Sin validación de coherencia con la categoría acá: que la unidad sea
        // válida PARA esa categoría es una matriz
        // (UnidadesMedidaArticulo.EsValidaParaCategoria) que mira dos propiedades a
        // la vez, y un atributo solo ve la suya. Lo valida el servicio.
        [Required(ErrorMessage = "La unidad de medida es obligatoria.")]
        [StringLength(LongitudesDonaciones.UnidadMedida)]
        public string UnidadMedida { get; set; } = string.Empty;
    }

    public class DonacionEspecieCrearDto
    {
        // Mismo par excluyente que en DonacionDineroCrearDto: donante existente o
        // donante nuevo, resuelto por el servicio.
        public int? DonanteId { get; set; }
        public NuevoDonanteDto? NuevoDonante { get; set; }

        public DateTime Fecha { get; set; }

        [StringLength(LongitudesDonaciones.Observaciones, ErrorMessage = "Las observaciones no pueden superar los {1} caracteres.")]
        public string? Observaciones { get; set; }

        // Al menos una línea: una donación en especie sin artículos no registra
        // nada. La BD no lo puede exigir (una cabecera sin filas hijas es válida
        // para el motor), así que esta es la única barrera automática.
        [MinLength(1, ErrorMessage = "Debe agregar al menos un artículo a la donación.")]
        public List<DetalleArticuloDto> Articulos { get; set; } = new();
    }

    // ----------------------------------------------------------------------
    // Donaciones entregadas
    // ----------------------------------------------------------------------

    public class DonacionEntregadaCrearDto
    {
        // [Range(1, ...)] y no [Required] para un int no-nullable: es el mismo
        // recurso que usa SalidaDonacionCrearDto para exigir que se haya
        // seleccionado algo, porque un int sin elegir llega como 0 y [Required]
        // lo daría por válido.
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un artículo.")]
        public int ArticuloId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Cantidad { get; set; }

        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "El tipo de destinatario es obligatorio.")]
        [StringLength(LongitudesDonaciones.TipoDestinatario)]
        public string TipoDestinatario { get; set; } = string.Empty;

        // Los dos nullable y mutuamente excluyentes, igual que en la entidad: cuál
        // de los dos se llena lo decide TipoDestinatario. No hay [Required] en
        // ninguno porque cada uno es obligatorio solo en la mitad de los casos;
        // esa condición cruzada la valida el servicio, y la respalda en la base el
        // CHECK CK_DonacionesEntregadas_Destinatario.
        public int? BeneficiarioId { get; set; }

        [StringLength(LongitudesDonaciones.ComunidadDestinataria, ErrorMessage = "La comunidad no puede superar los {1} caracteres.")]
        public string? ComunidadDestinataria { get; set; }

        [StringLength(LongitudesDonaciones.Observaciones, ErrorMessage = "Las observaciones no pueden superar los {1} caracteres.")]
        public string? Observaciones { get; set; }
    }

    // ----------------------------------------------------------------------
    // Historiales
    // ----------------------------------------------------------------------

    // Fila unificada del historial de donaciones RECIBIDAS: dinero y especie en la
    // misma grilla, distinguidas por TipoDonacion. Es el mismo criterio de
    // MovimientoInventarioDto, que unifica entradas y salidas en una sola fila.
    public class HistorialDonacionDto
    {
        public int Id { get; set; }

        // "Dinero" o "Especie". El Id de arriba es el de SU tabla, así que dos
        // filas de tipos distintos pueden compartir el mismo número: la fila se
        // identifica por el par (TipoDonacion, Id), nunca por el Id solo.
        public string TipoDonacion { get; set; } = string.Empty;

        public string NombreDonante { get; set; } = string.Empty;

        // Nullable porque solo las donaciones de dinero tienen monto. En una fila
        // de especie queda en null, que la grilla muestra vacío; un 0 se leería
        // como "donó cero colones", que es otra cosa.
        public decimal? Monto { get; set; }

        // Resumen legible de lo donado, que es lo único que las dos clases de
        // donación pueden mostrar en la misma columna: en especie, el detalle
        // ("3 Arroz, 2 Frijoles"); en dinero, las observaciones.
        public string Descripcion { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }
    }

    public class FiltrosHistorialDonacionDto
    {
        public int? DonanteId { get; set; }

        // "Dinero", "Especie" o null (ambas). Cuando trae un valor, el servicio se
        // ahorra la consulta de la clase que no se pidió.
        public string? TipoDonacion { get; set; }

        // Nombres FechaDesde/FechaHasta y no Desde/Hasta como en
        // FiltrosMovimientoDto: acá se filtra un solo campo de fecha y conviene que
        // el nombre lo diga, sobre todo al lado de DonanteId.
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }

    // Las filas más el total, en vez de solo la lista: el total de dinero se calcula
    // sobre TODAS las donaciones que cumplen el filtro y la grilla no podría
    // deducirlo de las filas que muestra. Mismo patrón que
    // HistorialMovimientosResultadoDto.
    public class HistorialDonacionesResultadoDto
    {
        public List<HistorialDonacionDto> Donaciones { get; set; } = new();

        // Solo suma las donaciones de dinero: las de especie no tienen monto (no se
        // valorizan los artículos donados), así que no hay nada que sumar de ellas.
        public decimal TotalDinero { get; set; }
    }

    public class HistorialDonacionEntregadaDto
    {
        public int Id { get; set; }

        // Nombre del artículo ya resuelto, no el ArticuloId: la grilla muestra
        // texto y así no tiene que ir a buscarlo. Mismo criterio que
        // MovimientoInventarioDto.Articulo.
        public string Articulo { get; set; } = string.Empty;

        public int Cantidad { get; set; }
        public string UnidadMedida { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string TipoDestinatario { get; set; } = string.Empty;

        // Una sola columna para las dos clases de destinatario: el servicio pone
        // acá el nombre del beneficiario o el de la comunidad, según
        // TipoDestinatario. La grilla no necesita saber de dónde salió.
        public string NombreDestinatario { get; set; } = string.Empty;

        public string? Observaciones { get; set; }
    }

    public class FiltrosHistorialEntregaDto
    {
        public int? ArticuloId { get; set; }

        // "Beneficiario", "Comunidad" o null (ambos).
        public string? TipoDestinatario { get; set; }

        // Complementa al anterior sin depender de él: filtrar por un beneficiario
        // concreto ya implica TipoDestinatario = "Beneficiario". Se apoya en
        // IX_DonacionesEntregadas_Beneficiario y responde "qué se le ha entregado a
        // esta persona" desde su ficha.
        public int? BeneficiarioId { get; set; }

        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }

    public class HistorialEntregasResultadoDto
    {
        public List<HistorialDonacionEntregadaDto> Entregas { get; set; } = new();

        // Suma de unidades entregadas. Es un número con la reserva de que mezcla
        // unidades de medida distintas (kilos con pares de zapatos): sirve como
        // volumen total de entregas, no como una magnitud física.
        public int TotalEntregado { get; set; }
    }
}
