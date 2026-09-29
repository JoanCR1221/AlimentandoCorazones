using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.Exceptions;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Validators
{
    // Datos de un alquiler ya validados y normalizados. Las listas de ids vienen sin
    // repetidos. Que esos ids existan y estén activos no se comprueba acá: necesita
    // consultar la base, así que lo hace AlquileresService.
    public sealed record AlquilerValidado(
        int ArrendatarioId,
        DateTime Fecha,
        TimeSpan HoraInicio,
        TimeSpan HoraFin,
        IReadOnlyList<int> EspacioIds,
        IReadOnlyList<int> CaracteristicaIds,
        int CantidadPersonas,
        decimal Monto,
        string Moneda,
        string? Observaciones);

    // Toda la validación de entrada del alquiler que no necesita la base. Las reglas
    // de horario salen de ReglasAlquiler; acá se aplican y se traducen a mensajes.
    public static class AlquilerValidator
    {
        public const int LongitudMaximaObservaciones = 500;
        public const int LongitudMaximaMotivoCancelacion = 500;

        // Mismo criterio que EspaciosValidator.CapacidadMaxima.
        public const int CantidadPersonasMaxima = 1000;

        // horario es el configurado (HorarioAlquiler); hoy es inyectable para las
        // pruebas y en producción es DateTime.Today.
        public static AlquilerValidado Validar(AlquilerCrearDto dto, HorarioAlquiler horario, DateTime? hoy = null)
        {
            if (dto.ArrendatarioId <= 0)
                throw new ValidationException("Debe seleccionar un arrendatario.");

            var error = DescribirErrorDeFechaYHoras(dto.Fecha, dto.HoraInicio, dto.HoraFin, hoy)
                        ?? DescribirErrorDeHorarioPermitido(dto.Fecha, dto.HoraInicio!.Value, dto.HoraFin!.Value, horario);
            if (error is not null)
                throw new ValidationException(error);

            var espacioIds = (dto.EspacioIds ?? new()).Distinct().ToList();
            if (espacioIds.Count == 0)
                throw new ValidationException("Debe elegir al menos un sector.");

            if (dto.CantidadPersonas < 1 || dto.CantidadPersonas > CantidadPersonasMaxima)
                throw new ValidationException(
                    $"La cantidad de personas debe estar entre 1 y {CantidadPersonasMaxima}.");

            return new AlquilerValidado(
                dto.ArrendatarioId,
                dto.Fecha.Date,
                dto.HoraInicio!.Value,
                dto.HoraFin!.Value,
                espacioIds,
                (dto.CaracteristicaIds ?? new()).Distinct().ToList(),
                dto.CantidadPersonas,
                ValidarMonto(dto.Monto),
                ValidarMoneda(dto.Moneda),
                ValidarObservaciones(dto.Observaciones));
        }

        // Primer problema de fecha u horas que no depende del horario configurado, o
        // null si no hay. Lo usa también AlquilerCrearDto.Validate para que el
        // formulario avise con el mismo texto que después daría el servidor.
        public static string? DescribirErrorDeFechaYHoras(
            DateTime fecha, TimeSpan? horaInicio, TimeSpan? horaFin, DateTime? hoy = null)
        {
            if (fecha == default)
                return "La fecha es obligatoria.";

            if (!ReglasAlquiler.EsFechaValidaParaReservar(fecha, hoy))
                return "No se puede reservar un espacio para una fecha que ya pasó.";

            if (horaInicio is null || horaFin is null)
                return "La hora de inicio y la hora de fin son obligatorias.";

            if (horaFin <= horaInicio)
                return "La hora de fin debe ser posterior a la hora de inicio.";

            if ((horaFin.Value - horaInicio.Value).TotalMinutes < ReglasAlquiler.DuracionMinimaMinutos)
                return $"El alquiler debe durar al menos {ReglasAlquiler.DuracionMinimaMinutos} minutos.";

            return null;
        }

        // Aparte de lo anterior porque necesita el horario configurado, que vive en
        // la base: el DTO no lo conoce. La pantalla de registro lo carga y usa este
        // mismo método para avisar antes de guardar.
        public static string? DescribirErrorDeHorarioPermitido(
            DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin, HorarioAlquiler horario)
        {
            if (!ReglasAlquiler.EstaDentroDelHorario(fecha, horaInicio, horaFin, horario))
                return $"El horario está fuera de lo permitido. {ReglasAlquiler.DescribirHorario(fecha, horario)}";

            return null;
        }

        // Valida un horario configurado antes de guardarlo: las cuatro horas
        // obligatorias y cada franja con al menos la duración mínima de un alquiler
        // (un cierre anterior a la apertura dejaría ese día sin poder alquilarse).
        public static HorarioAlquiler ValidarHorario(
            TimeSpan? aperturaEntreSemana, TimeSpan? cierreEntreSemana,
            TimeSpan? aperturaFinDeSemana, TimeSpan? cierreFinDeSemana)
        {
            var (aperturaLv, cierreLv) = ValidarFranja(aperturaEntreSemana, cierreEntreSemana, "de lunes a viernes");
            var (aperturaSd, cierreSd) = ValidarFranja(aperturaFinDeSemana, cierreFinDeSemana, "de sábado y domingo");

            return new HorarioAlquiler
            {
                Id = HorarioAlquiler.IdUnico,
                AperturaEntreSemana = aperturaLv,
                CierreEntreSemana = cierreLv,
                AperturaFinDeSemana = aperturaSd,
                CierreFinDeSemana = cierreSd
            };
        }

        private static (TimeSpan Apertura, TimeSpan Cierre) ValidarFranja(TimeSpan? apertura, TimeSpan? cierre, string dias)
        {
            if (apertura is null || cierre is null)
                throw new ValidationException($"La hora de apertura y la de cierre {dias} son obligatorias.");

            // Solo horas y minutos: un TimeSpan con segundos o de más de un día no
            // tiene sentido como hora del reloj.
            var inicio = new TimeSpan(apertura.Value.Hours, apertura.Value.Minutes, 0);
            var fin = new TimeSpan(cierre.Value.Hours, cierre.Value.Minutes, 0);

            if ((fin - inicio).TotalMinutes < ReglasAlquiler.DuracionMinimaMinutos)
                throw new ValidationException(
                    $"El cierre {dias} tiene que ser al menos {ReglasAlquiler.DuracionMinimaMinutos} minutos después de la apertura.");

            return (inicio, fin);
        }

        public static decimal ValidarMonto(decimal monto)
        {
            if (monto < 0)
                throw new ValidationException("El monto no puede ser negativo.");

            return monto;
        }

        // Se elige de una lista, pero se comprueba igual contra el catálogo cerrado:
        // mismo criterio que GastoOperativoValidator.ValidarMoneda.
        public static string ValidarMoneda(string? moneda)
        {
            var normalizada = TextoNormalizador.CompactarEspacios(moneda);

            if (normalizada.Length == 0)
                throw new ValidationException("La moneda es obligatoria.");

            if (!TiposMoneda.EsValido(normalizada))
                throw new ValidationException(
                    $"La moneda '{normalizada}' no es válida. Valores válidos: {string.Join(", ", TiposMoneda.Todos)}.");

            return normalizada;
        }

        public static string? ValidarObservaciones(string? observaciones)
        {
            var normalizadas = TextoNormalizador.CompactarEspacios(observaciones);

            if (normalizadas.Length > LongitudMaximaObservaciones)
                throw new ValidationException(
                    $"Las observaciones no pueden superar los {LongitudMaximaObservaciones} caracteres.");

            return normalizadas.Length == 0 ? null : normalizadas;
        }

        public static string ValidarMotivoCancelacion(string? motivo)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(motivo);

            if (normalizado.Length == 0)
                throw new ValidationException("El motivo de cancelación es obligatorio.");

            if (normalizado.Length > LongitudMaximaMotivoCancelacion)
                throw new ValidationException(
                    $"El motivo de cancelación no puede superar los {LongitudMaximaMotivoCancelacion} caracteres.");

            return normalizado;
        }
    }
}
