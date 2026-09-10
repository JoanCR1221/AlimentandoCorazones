using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    // Datos de un gasto ya validados y normalizados, listos para persistir.
    // El servicio solo los copia a la entidad: no vuelve a limpiar ni a decidir nada.
    public sealed record GastoOperativoValidado(
        string Categoria,
        decimal Monto,
        DateTime Fecha,
        string Descripcion,
        string Responsable);

    // Toda la validación y normalización de entrada del gasto, centralizada por
    // el mismo motivo que ArticuloValidator y BeneficiarioValidator.
    //
    // Los largos máximos son un valor de trabajo mientras Base de datos no
    // configure GastoOperativo en SigacDbContext (AB#2489/2491/2494): hay que
    // confirmarlos contra las columnas reales cuando esa tarea se entregue.
    public static class GastoOperativoValidator
    {
        public const int LongitudMaximaDescripcion = 500;
        public const int LongitudMaximaResponsable = 150;
        public const int LongitudMaximaMotivoAnulacion = 500;

        public static GastoOperativoValidado Validar(GastoOperativoCrearDto dto) =>
            Validar(dto.Categoria, dto.Monto, dto.Fecha, dto.Descripcion, dto.Responsable);

        public static GastoOperativoValidado Validar(GastoOperativoEditarDto dto) =>
            Validar(dto.Categoria, dto.Monto, dto.Fecha, dto.Descripcion, dto.Responsable);

        private static GastoOperativoValidado Validar(
            string? categoria, decimal monto, DateTime fecha, string? descripcion, string? responsable)
        {
            return new GastoOperativoValidado(
                ValidarCategoria(categoria),
                ValidarMonto(monto),
                ValidarFecha(fecha),
                ValidarDescripcion(descripcion),
                ValidarResponsable(responsable));
        }

        public static string ValidarCategoria(string? categoria)
        {
            var normalizada = TextoNormalizador.CompactarEspacios(categoria);

            if (normalizada.Length == 0)
                throw new ValidationException("La categoría es obligatoria.");

            if (!CategoriasGastoOperativo.EsValido(normalizada))
            {
                throw new ValidationException(
                    $"La categoría '{normalizada}' no es válida. " +
                    $"Categorías válidas: {string.Join(", ", CategoriasGastoOperativo.Todos)}.");
            }

            return normalizada;
        }

        public static decimal ValidarMonto(decimal monto)
        {
            if (monto <= 0)
                throw new ValidationException("El monto debe ser mayor a 0.");

            return monto;
        }

        // Igual que la fecha de una entrada de inventario: el gasto registra algo
        // que ya ocurrió, así que una fecha futura no representa un movimiento real.
        public static DateTime ValidarFecha(DateTime fecha)
        {
            if (fecha == default)
                throw new ValidationException("La fecha es obligatoria.");

            if (fecha.Date > DateTime.Today)
                throw new ValidationException("La fecha del gasto no puede ser futura.");

            return fecha.Date;
        }

        public static string ValidarDescripcion(string? descripcion)
        {
            var normalizada = TextoNormalizador.CompactarEspacios(descripcion);

            if (normalizada.Length == 0)
                throw new ValidationException("La descripción es obligatoria.");

            if (normalizada.Length > LongitudMaximaDescripcion)
                throw new ValidationException(
                    $"La descripción no puede superar los {LongitudMaximaDescripcion} caracteres.");

            return normalizada;
        }

        public static string ValidarResponsable(string? responsable)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(responsable);

            if (normalizado.Length == 0)
                throw new ValidationException("El responsable es obligatorio.");

            if (normalizado.Length > LongitudMaximaResponsable)
                throw new ValidationException(
                    $"El responsable no puede superar los {LongitudMaximaResponsable} caracteres.");

            return normalizado;
        }

        public static string ValidarMotivoAnulacion(string? motivo)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(motivo);

            if (normalizado.Length == 0)
                throw new ValidationException("El motivo de anulación es obligatorio.");

            if (normalizado.Length > LongitudMaximaMotivoAnulacion)
                throw new ValidationException(
                    $"El motivo de anulación no puede superar los {LongitudMaximaMotivoAnulacion} caracteres.");

            return normalizado;
        }
    }
}
