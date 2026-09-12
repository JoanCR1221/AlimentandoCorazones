using SIGAC.Application.DTOs.Proyectos;
using SIGAC.Application.Exceptions;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Validators
{
    // Datos de un proyecto ya validados y normalizados, listos para persistir.
    // El servicio solo los copia a la entidad: no vuelve a limpiar ni a decidir nada.
    public sealed record ProyectoValidado(
        string Nombre,
        string Descripcion,
        DateTime FechaInicio,
        DateTime FechaEstimadaFin);

    // Toda la validación y normalización de entrada del proyecto, centralizada por
    // el mismo motivo que GastoOperativoValidator.
    //
    // Los largos máximos coinciden con las columnas reales de
    // ProyectosComunitarios: si cambia una columna, hay que mover también la
    // constante de acá.
    public static class ProyectoValidator
    {
        public const int LongitudMaximaNombre = 150;
        public const int LongitudMaximaDescripcion = 500;

        public static ProyectoValidado Validar(ProyectoCrearDto dto) =>
            Validar(dto.Nombre, dto.Descripcion, dto.FechaInicio, dto.FechaEstimadaFin);

        public static ProyectoValidado Validar(ProyectoEditarDto dto) =>
            Validar(dto.Nombre, dto.Descripcion, dto.FechaInicio, dto.FechaEstimadaFin);

        private static ProyectoValidado Validar(
            string? nombre, string? descripcion, DateTime fechaInicio, DateTime fechaEstimadaFin)
        {
            var (inicio, estimadaFin) = ValidarFechas(fechaInicio, fechaEstimadaFin);

            return new ProyectoValidado(
                ValidarNombre(nombre),
                ValidarDescripcion(descripcion),
                inicio,
                estimadaFin);
        }

        public static string ValidarNombre(string? nombre)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(nombre);

            if (normalizado.Length == 0)
                throw new ValidationException("El nombre del proyecto es obligatorio.");

            if (normalizado.Length > LongitudMaximaNombre)
                throw new ValidationException(
                    $"El nombre no puede superar los {LongitudMaximaNombre} caracteres.");

            return normalizado;
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

        public static (DateTime Inicio, DateTime EstimadaFin) ValidarFechas(
            DateTime fechaInicio, DateTime fechaEstimadaFin)
        {
            if (fechaInicio == default)
                throw new ValidationException("La fecha de inicio es obligatoria.");

            if (fechaEstimadaFin == default)
                throw new ValidationException("La fecha estimada de finalización es obligatoria.");

            if (fechaEstimadaFin.Date < fechaInicio.Date)
                throw new ValidationException(
                    "La fecha estimada de finalización no puede ser anterior a la fecha de inicio.");

            return (fechaInicio.Date, fechaEstimadaFin.Date);
        }

        // Finalizado queda reservado para FinalizarProyectoAsync: esa transición
        // registra además la fecha de finalización real, algo que la edición
        // genérica no hace. Planificado, EnCurso y Cancelado sí se mueven
        // libremente a través de editar.
        public static EstadoProyecto ValidarEstadoParaEdicion(EstadoProyecto estado)
        {
            if (!Enum.IsDefined(estado))
                throw new ValidationException("El estado del proyecto no es válido.");

            if (estado == EstadoProyecto.Finalizado)
                throw new ValidationException(
                    "No se puede marcar un proyecto como finalizado mediante edición; use la acción de finalizar.");

            return estado;
        }
    }
}
