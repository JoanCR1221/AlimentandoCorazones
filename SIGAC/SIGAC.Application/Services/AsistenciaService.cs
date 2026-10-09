using SIGAC.Application.DTOs.Asistencia;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    public class AsistenciaService : IAsistenciaService
    {
        private readonly IAsistenciaRepository _asistenciaRepository;
        private readonly IBeneficiariosRepository _beneficiariosRepository;
        private readonly IBitacoraService _bitacora;

        public AsistenciaService(
            IAsistenciaRepository asistenciaRepository,
            IBeneficiariosRepository beneficiariosRepository,
            IBitacoraService bitacora)
        {
            _asistenciaRepository = asistenciaRepository;
            _beneficiariosRepository = beneficiariosRepository;
            _bitacora = bitacora;
        }

        public async Task RegistrarAsistenciaAsync(AsistenciaCrearDto dto)
        {
            try
            {
                if (dto.Fecha == default)
                    throw new ValidationException("La fecha de asistencia es obligatoria.");

                // Se compara sin hora y contra una sola fuente de "hoy", la misma que
                // usa el calendario de la pantalla: si no, una fecha elegida al
                // mediodía podría verse como futura frente a una medianoche.
                var hoy = ReglasAsistencia.Hoy;
                var fecha = dto.Fecha.Date;

                if (fecha > hoy)
                    throw new ValidationException("La fecha de asistencia no puede ser futura.");

                // La pantalla ya acota el calendario, pero la regla tiene que estar
                // acá: cualquier otro camino de entrada se saltearía la UI.
                var fechaMinima = ReglasAsistencia.FechaMinimaRegistro(hoy);

                if (fecha < fechaMinima)
                    throw new ValidationException(
                        $"Solo se pueden registrar asistencias de los últimos {ReglasAsistencia.MaximoDiasHaciaAtras} días. " +
                        $"La fecha más antigua permitida es {fechaMinima:dd/MM/yyyy}.");

                if (!TiemposComida.EsValido(dto.TiempoComida))
                    throw new ValidationException(
                        $"El tiempo de comida debe ser uno de: {string.Join(", ", TiemposComida.Todos)}.");

                var beneficiario = await _beneficiariosRepository.ObtenerPorIdAsync(dto.BeneficiarioId)
                    ?? throw new NotFoundException("El beneficiario no existe.");

                if (!beneficiario.Estado)
                    throw new ValidationException("El beneficiario no está activo.");

                if (await _asistenciaRepository.ExisteAsistenciaAsync(dto.BeneficiarioId, fecha, dto.TiempoComida))
                    throw new DuplicateException("Ya existe asistencia registrada para ese beneficiario, fecha y tiempo de comida.");

                var asistenciasDelDia = await _asistenciaRepository.ObtenerAsistenciasDiariasAsync(fecha);
                var registrosBeneficiario = asistenciasDelDia.Count(a => a.BeneficiarioId == dto.BeneficiarioId);

                if (registrosBeneficiario >= ReglasAsistencia.MaximoRegistrosPorDia)
                    throw new ValidationException(
                        $"El beneficiario ya alcanzó el máximo de {ReglasAsistencia.MaximoRegistrosPorDia} registros para este día.");

                var asistencia = new AsistenciaComedor
                {
                    BeneficiarioId = dto.BeneficiarioId,
                    Fecha = fecha,
                    TiempoComida = dto.TiempoComida
                };

                await _asistenciaRepository.AgregarAsync(asistencia);

                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Asistencia,
                    $"{beneficiario.NombreCompleto} (#{beneficiario.Id}), {fecha:dd/MM/yyyy}, {dto.TiempoComida}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DuplicateException)
            {
                throw new Exception("Error al registrar la asistencia.", ex);
            }
        }

        public async Task<bool> ExisteAsistenciaAsync(int beneficiarioId, DateTime fecha, string tiempoComida)
        {
            try
            {
                // Con los datos incompletos no hay nada que verificar todavía: la
                // pantalla llama a esto mientras el formulario se está llenando.
                if (beneficiarioId <= 0 || fecha == default || !TiemposComida.EsValido(tiempoComida))
                    return false;

                return await _asistenciaRepository.ExisteAsistenciaAsync(beneficiarioId, fecha, tiempoComida);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al verificar la asistencia.", ex);
            }
        }

        public async Task<HistorialAsistenciaResultadoDto> ObtenerHistorialAsistenciaAsync(FiltrosAsistenciaDto filtros)
        {
            try
            {
                // Los totales cubren todo el período filtrado (un GROUP BY en la
                // base), no solo la página: contar las filas de la página daría
                // como máximo el tamaño de página. El total de registros es la suma
                // de esos totales, así no hace falta un COUNT aparte.
                var totales = await _asistenciaRepository.ObtenerTotalesPorTiempoComidaAsync(filtros);
                var totalRegistros = totales.Values.Sum();

                // Sin registros no hay página que pedir: se ahorra la segunda consulta.
                var asistencias = totalRegistros == 0
                    ? Array.Empty<AsistenciaComedor>()
                    : await _asistenciaRepository.ObtenerPaginaHistorialAsync(filtros);

                // El nombre sale de la navegación que el repositorio ya trajo con
                // Include, en el mismo viaje a la base: antes era una consulta extra
                // por cada beneficiario distinto del período. El orden ya viene de
                // SQL (el que fija el Skip/Take); reordenar acá lo desharía.
                var registros = asistencias
                    .Select(a => new HistorialAsistenciaDto
                    {
                        Id = a.Id,
                        NombreBeneficiario = a.Beneficiario?.NombreCompleto ?? string.Empty,
                        Fecha = a.Fecha,
                        TiempoComida = a.TiempoComida
                    })
                    .ToList();

                return new HistorialAsistenciaResultadoDto
                {
                    Registros = registros,
                    TotalRegistros = totalRegistros,
                    TotalesPorTiempoComida = OrdenarPorTiempoComida(totales)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el historial de asistencia.", ex);
            }
        }

        // En el orden del día (desayuno, almuerzo, merienda) y no en el que devuelva la
        // base: antes el orden dependía de cuál comida aparecía primero en el período.
        private static Dictionary<string, int> OrdenarPorTiempoComida(IReadOnlyDictionary<string, int> totales)
        {
            // Un valor que no esté en la lista (dato viejo) va al final, no se pierde.
            int Posicion(string tiempoComida)
            {
                for (var i = 0; i < TiemposComida.Todos.Count; i++)
                {
                    if (TiemposComida.Todos[i] == tiempoComida)
                        return i;
                }

                return int.MaxValue;
            }

            return totales
                .OrderBy(kv => Posicion(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }
}