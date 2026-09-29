using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Validators;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    // Orquesta el registro, la consulta y la cancelación de alquileres: valida con
    // AlquilerValidator, comprueba contra la base lo que el validador no puede
    // (arrendatario, sectores y características existentes y activos, capacidad y
    // choques de horario) y delega en el repositorio.
    public class AlquileresService : IAlquileresService
    {
        private readonly IAlquileresRepository _repository;
        private readonly IArrendatariosRepository _arrendatariosRepository;
        private readonly IEspaciosRepository _espaciosRepository;
        private readonly IBitacoraService _bitacora;

        public AlquileresService(
            IAlquileresRepository repository,
            IArrendatariosRepository arrendatariosRepository,
            IEspaciosRepository espaciosRepository,
            IBitacoraService bitacora)
        {
            _repository = repository;
            _arrendatariosRepository = arrendatariosRepository;
            _espaciosRepository = espaciosRepository;
            _bitacora = bitacora;
        }

        public async Task<int> RegistrarAlquilerAsync(AlquilerCrearDto dto)
        {
            try
            {
                // El horario permitido es configurable: se lee en cada registro para
                // validar siempre contra el vigente.
                var horario = await _espaciosRepository.ObtenerHorarioAsync();
                var datos = AlquilerValidator.Validar(dto, horario);

                var arrendatario = await _arrendatariosRepository.ObtenerPorIdAsync(datos.ArrendatarioId)
                    ?? throw new NotFoundException("El arrendatario no existe.");

                if (!arrendatario.Estado)
                    throw new ValidationException(
                        $"El arrendatario '{arrendatario.Nombre}' está inactivo y no puede registrar alquileres.");

                var espacios = await _espaciosRepository.ObtenerEspaciosPorIdsAsync(datos.EspacioIds);
                ValidarActivos(datos.EspacioIds, espacios.Select(e => (e.Id, e.Nombre, e.Estado)),
                    "Uno de los sectores seleccionados ya no existe.");

                var caracteristicas = await _espaciosRepository.ObtenerCaracteristicasPorIdsAsync(datos.CaracteristicaIds);
                ValidarActivos(datos.CaracteristicaIds, caracteristicas.Select(c => (c.Id, c.Nombre, c.Estado)),
                    "Una de las características seleccionadas ya no existe.");

                ValidarCapacidad(datos.CantidadPersonas, espacios);

                // Primer chequeo de choques, para dar un mensaje que diga con quién se
                // choca. El repositorio lo repite dentro de la transacción del INSERT.
                var choques = await _repository.ObtenerChoquesAsync(
                    datos.Fecha, datos.HoraInicio, datos.HoraFin, datos.EspacioIds);

                if (choques.Count > 0)
                    throw new ValidationException(DescribirChoque(choques[0], datos.EspacioIds));

                var alquiler = new AlquilerEspacio
                {
                    ArrendatarioId = arrendatario.Id,
                    Fecha = datos.Fecha,
                    HoraInicio = datos.HoraInicio,
                    HoraFin = datos.HoraFin,
                    CantidadPersonas = datos.CantidadPersonas,
                    Monto = datos.Monto,
                    Moneda = datos.Moneda,
                    Estado = EstadoAlquiler.Reservado,
                    Observaciones = datos.Observaciones,
                    FechaRegistro = DateTime.Now,
                    Espacios = espacios.ToList(),
                    Caracteristicas = caracteristicas.ToList()
                };

                await _repository.AgregarAlquilerAsync(alquiler);

                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Alquileres,
                    $"Alquiler #{alquiler.Id}: {arrendatario.Nombre}, {alquiler.Fecha:dd/MM/yyyy} " +
                    $"{ReglasAlquiler.FormatearFranja(alquiler.HoraInicio, alquiler.HoraFin)} " +
                    $"({string.Join(", ", espacios.Select(e => e.Nombre))})");

                return alquiler.Id;
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al registrar el alquiler.", ex);
            }
        }

        public async Task<HistorialAlquileresResultadoDto> ObtenerHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros)
        {
            try
            {
                var alquileres = await _repository.ObtenerHistorialAlquileresAsync(filtros);

                var filas = alquileres
                    .Select(a => new AlquilerListaDto
                    {
                        Id = a.Id,
                        Fecha = a.Fecha,
                        HoraInicio = a.HoraInicio,
                        HoraFin = a.HoraFin,
                        Arrendatario = a.Arrendatario?.Nombre ?? $"Arrendatario #{a.ArrendatarioId}",
                        TelefonoArrendatario = ReglasTelefono.Formatear(a.Arrendatario?.CodigoPaisTelefono, a.Arrendatario?.Telefono),
                        Espacios = a.Espacios.Select(e => e.Nombre).OrderBy(n => n).ToList(),
                        Caracteristicas = a.Caracteristicas.Select(c => c.Nombre).OrderBy(n => n).ToList(),
                        CantidadPersonas = a.CantidadPersonas,
                        Monto = a.Monto,
                        Moneda = a.Moneda,
                        Estado = a.Estado,
                        MotivoCancelacion = a.MotivoCancelacion,
                        Observaciones = a.Observaciones
                    })
                    .ToList();

                return new HistorialAlquileresResultadoDto
                {
                    Alquileres = filas,
                    TotalesPorMoneda = filas
                        .Where(f => f.Estado == EstadoAlquiler.Reservado)
                        .GroupBy(f => f.Moneda)
                        .Select(g => new MontoPorMonedaDto(g.Key, g.Sum(f => f.Monto)))
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar los alquileres.", ex);
            }
        }

        public async Task CancelarAlquilerAsync(CancelacionAlquilerDto dto)
        {
            try
            {
                var motivo = AlquilerValidator.ValidarMotivoCancelacion(dto.MotivoCancelacion);

                var alquiler = await _repository.ObtenerPorIdAsync(dto.AlquilerId)
                    ?? throw new NotFoundException("El alquiler no existe.");

                if (alquiler.Estado == EstadoAlquiler.Cancelado)
                    throw new ValidationException("El alquiler ya está cancelado.");

                // El repositorio vuelve a exigir Estado = Reservado en el mismo
                // UPDATE: si otro usuario lo canceló entre la lectura y acá, se avisa
                // en vez de pisar su motivo.
                if (!await _repository.CancelarAsync(alquiler.Id, motivo))
                    throw new ValidationException("El alquiler ya fue cancelado por otro usuario.");

                await _bitacora.RegistrarAsync(AccionesBitacora.Anular, ModulosSistema.Alquileres,
                    $"Alquiler #{alquiler.Id} cancelado ({alquiler.Fecha:dd/MM/yyyy}): {motivo}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al cancelar el alquiler.", ex);
            }
        }

        // Todo id pedido tiene que existir y estar activo. Un sector inactivo (en
        // reparación, por ejemplo) no se ofrece en la pantalla, pero se comprueba
        // igual porque la pantalla pudo quedar abierta desde antes de desactivarlo.
        private static void ValidarActivos(
            IReadOnlyList<int> idsPedidos, IEnumerable<(int Id, string Nombre, bool Estado)> encontrados, string mensajeNoExiste)
        {
            var porId = encontrados.ToDictionary(e => e.Id);

            foreach (var id in idsPedidos)
            {
                if (!porId.TryGetValue(id, out var encontrado))
                    throw new NotFoundException(mensajeNoExiste);

                if (!encontrado.Estado)
                    throw new ValidationException($"'{encontrado.Nombre}' está desactivado y no se puede seleccionar.");
            }
        }

        // La capacidad del alquiler es la suma de la de los sectores elegidos que la
        // declaran (Área de juego + Sala de servicio). Los que no la declaran
        // (Baños, Cocina) no suman ni restan. Si ninguno la declara, no hay contra
        // qué comparar y no se valida.
        private static void ValidarCapacidad(int cantidadPersonas, IReadOnlyList<EspacioFisico> espacios)
        {
            var conCapacidad = espacios.Where(e => e.Capacidad.HasValue).ToList();
            if (conCapacidad.Count == 0)
                return;

            var capacidad = conCapacidad.Sum(e => e.Capacidad!.Value);

            if (cantidadPersonas > capacidad)
                throw new ValidationException(
                    $"La cantidad de personas ({cantidadPersonas}) supera la capacidad de los sectores elegidos ({capacidad}).");
        }

        private static string DescribirChoque(AlquilerEspacio choque, IReadOnlyList<int> espacioIds)
        {
            var sectores = choque.Espacios
                .Where(e => espacioIds.Contains(e.Id))
                .Select(e => e.Nombre);

            return $"El horario choca con otro alquiler de {string.Join(", ", sectores)}: " +
                   $"{choque.Arrendatario?.Nombre ?? $"arrendatario #{choque.ArrendatarioId}"}, " +
                   $"de {ReglasAlquiler.FormatearHora(choque.HoraInicio)} a {ReglasAlquiler.FormatearHora(choque.HoraFin)}";
        }
    }
}
