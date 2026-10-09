using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.DTOs.Bitacora;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // Registro y cancelación de alquileres: horarios, choques entre sectores,
    // capacidad y estados. Con repositorios en memoria, mismo criterio que
    // ProyectosServiceTests.
    public class AlquileresServiceTests
    {
        private const int AreaDeJuego = 1;
        private const int SalaDeServicio = 2;
        private const int Banos = 3;
        private const int SectorInactivo = 9;
        private const int Luz = 1;

        private readonly RepositorioAlquileresFalso _alquileres = new();
        private readonly RepositorioArrendatariosFalso _arrendatarios = new();
        private readonly RepositorioEspaciosFalso _espacios = new();
        private readonly BitacoraFalsa _bitacora = new();
        private readonly AlquileresService _servicio;

        // Fechas siempre futuras: el servicio valida contra DateTime.Today.
        private static readonly DateTime ProximoLunes = Proximo(DayOfWeek.Monday);
        private static readonly DateTime ProximoSabado = Proximo(DayOfWeek.Saturday);

        public AlquileresServiceTests()
        {
            _servicio = new AlquileresService(_alquileres, _arrendatarios, _espacios, _bitacora);

            _arrendatarios.Datos.Add(new Arrendatario { Id = 1, Nombre = "Ana Rojas", Estado = true });
            _arrendatarios.Datos.Add(new Arrendatario { Id = 2, Nombre = "Grupo Scout", Estado = true });
            _arrendatarios.Datos.Add(new Arrendatario { Id = 3, Nombre = "Inactivo", Estado = false });

            _espacios.Espacios.Add(new EspacioFisico { Id = AreaDeJuego, Nombre = "Área de juego", Capacidad = 30, Estado = true });
            _espacios.Espacios.Add(new EspacioFisico { Id = SalaDeServicio, Nombre = "Sala de servicio", Capacidad = 50, Estado = true });
            _espacios.Espacios.Add(new EspacioFisico { Id = Banos, Nombre = "Baños", Estado = true });
            _espacios.Espacios.Add(new EspacioFisico { Id = SectorInactivo, Nombre = "Bodega", Estado = false });

            _espacios.Caracteristicas.Add(new CaracteristicaEspacio { Id = Luz, Nombre = "Luz", Estado = true });
        }

        private static DateTime Proximo(DayOfWeek dia)
        {
            var fecha = DateTime.Today.AddDays(1);
            while (fecha.DayOfWeek != dia)
                fecha = fecha.AddDays(1);
            return fecha;
        }

        private static AlquilerCrearDto Alquiler(
            DateTime fecha, int inicio, int fin, int arrendatarioId = 1, int personas = 20, int[]? espacios = null) => new()
        {
            ArrendatarioId = arrendatarioId,
            Fecha = fecha,
            HoraInicio = new TimeSpan(inicio, 0, 0),
            HoraFin = new TimeSpan(fin, 0, 0),
            EspacioIds = espacios?.ToList() ?? new List<int> { SalaDeServicio },
            CantidadPersonas = personas,
            Monto = 25000,
            Moneda = TiposMoneda.Colones
        };

        // ---- Registro ----

        [Fact]
        public async Task Registrar_un_alquiler_valido_lo_guarda_con_sus_sectores_y_lo_anota_en_la_bitacora()
        {
            var dto = Alquiler(ProximoLunes, 10, 14, espacios: new[] { SalaDeServicio, Banos });
            dto.CaracteristicaIds = new List<int> { Luz };

            var id = await _servicio.RegistrarAlquilerAsync(dto);

            var guardado = Assert.Single(_alquileres.Datos);
            Assert.Equal(id, guardado.Id);
            Assert.Equal(EstadoAlquiler.Reservado, guardado.Estado);
            Assert.Equal(new[] { SalaDeServicio, Banos }, guardado.Espacios.Select(e => e.Id).OrderBy(i => i));
            Assert.Equal(Luz, Assert.Single(guardado.Caracteristicas).Id);
            Assert.Equal(AccionesBitacora.Registrar, Assert.Single(_bitacora.Registros).Accion);
        }

        [Fact]
        public async Task Un_prestamo_sin_costo_se_puede_registrar()
        {
            var dto = Alquiler(ProximoLunes, 10, 12);
            dto.Monto = 0;

            await _servicio.RegistrarAlquilerAsync(dto);

            Assert.Equal(0, Assert.Single(_alquileres.Datos).Monto);
        }

        [Fact]
        public async Task Mismo_sector_en_horas_que_se_superponen_choca()
        {
            await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 14, espacios: new[] { SalaDeServicio }));

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 13, 16, arrendatarioId: 2, espacios: new[] { SalaDeServicio })));

            Assert.Contains("Sala de servicio", ex.Message);
            Assert.Contains("Ana Rojas", ex.Message);
            Assert.Single(_alquileres.Datos);
        }

        [Fact]
        public async Task Basta_con_compartir_un_sector_para_chocar()
        {
            await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 14, espacios: new[] { AreaDeJuego, Banos }));

            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 11, 12, arrendatarioId: 2, espacios: new[] { SalaDeServicio, Banos })));
        }

        [Fact]
        public async Task Alquileres_seguidos_en_el_mismo_sector_no_chocan()
        {
            await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 12));
            await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 12, 14, arrendatarioId: 2));

            Assert.Equal(2, _alquileres.Datos.Count);
        }

        [Fact]
        public async Task El_mismo_horario_en_otro_sector_no_choca()
        {
            await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 14, espacios: new[] { SalaDeServicio }));
            await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 14, arrendatarioId: 2, espacios: new[] { AreaDeJuego }));

            Assert.Equal(2, _alquileres.Datos.Count);
        }

        [Fact]
        public async Task Un_alquiler_cancelado_libera_el_horario()
        {
            var id = await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 14));
            await _servicio.CancelarAlquilerAsync(new CancelacionAlquilerDto { AlquilerId = id, MotivoCancelacion = "Se pospuso" });

            await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 14, arrendatarioId: 2));

            Assert.Equal(2, _alquileres.Datos.Count);
        }

        [Fact]
        public async Task El_sabado_no_se_puede_alquilar_despues_de_las_17()
        {
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarAlquilerAsync(Alquiler(ProximoSabado, 15, 19)));

            Assert.Contains("Sábado y domingo", ex.Message);
            Assert.Empty(_alquileres.Datos);
        }

        [Fact]
        public async Task Si_la_asociacion_extiende_el_horario_del_sabado_la_misma_franja_se_acepta()
        {
            _espacios.Horario.CierreFinDeSemana = new TimeSpan(20, 0, 0);

            await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoSabado, 15, 19));

            Assert.Single(_alquileres.Datos);
        }

        [Fact]
        public async Task El_lunes_si_se_puede_alquilar_hasta_las_20()
        {
            await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 15, 19));

            Assert.Single(_alquileres.Datos);
        }

        [Fact]
        public async Task No_se_reserva_una_fecha_pasada()
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarAlquilerAsync(Alquiler(DateTime.Today.AddDays(-1), 10, 12)));
        }

        [Fact]
        public async Task La_hora_de_fin_tiene_que_ser_posterior_a_la_de_inicio()
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 14, 10)));
        }

        [Fact]
        public async Task No_se_admiten_mas_personas_que_la_capacidad_de_los_sectores_elegidos()
        {
            // Área de juego (30) + Sala de servicio (50) = 80. Baños no declara
            // capacidad y no suma.
            await _servicio.RegistrarAlquilerAsync(
                Alquiler(ProximoLunes, 10, 12, personas: 80, espacios: new[] { AreaDeJuego, SalaDeServicio, Banos }));

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarAlquilerAsync(
                    Alquiler(ProximoLunes, 14, 16, personas: 81, espacios: new[] { AreaDeJuego, SalaDeServicio, Banos })));

            Assert.Contains("80", ex.Message);
        }

        [Fact]
        public async Task Sin_sectores_con_capacidad_no_se_valida_la_cantidad()
        {
            await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 12, personas: 500, espacios: new[] { Banos }));

            Assert.Single(_alquileres.Datos);
        }

        [Fact]
        public async Task Un_sector_desactivado_no_se_puede_alquilar()
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 12, espacios: new[] { SectorInactivo })));
        }

        [Fact]
        public async Task Un_arrendatario_inactivo_no_puede_alquilar()
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 12, arrendatarioId: 3)));
        }

        [Fact]
        public async Task Un_arrendatario_inexistente_avisa_que_no_existe()
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 12, arrendatarioId: 99)));
        }

        // ---- Cancelación ----

        [Fact]
        public async Task Cancelar_guarda_el_motivo_y_lo_anota_en_la_bitacora()
        {
            var id = await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 12));

            await _servicio.CancelarAlquilerAsync(new CancelacionAlquilerDto { AlquilerId = id, MotivoCancelacion = "  Lluvia  " });

            var alquiler = Assert.Single(_alquileres.Datos);
            Assert.Equal(EstadoAlquiler.Cancelado, alquiler.Estado);
            Assert.Equal("Lluvia", alquiler.MotivoCancelacion);
            Assert.Equal(AccionesBitacora.Anular, _bitacora.Registros.Last().Accion);
        }

        [Fact]
        public async Task Cancelar_exige_motivo()
        {
            var id = await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 12));

            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.CancelarAlquilerAsync(new CancelacionAlquilerDto { AlquilerId = id, MotivoCancelacion = " " }));

            Assert.Equal(EstadoAlquiler.Reservado, Assert.Single(_alquileres.Datos).Estado);
        }

        [Fact]
        public async Task No_se_cancela_dos_veces()
        {
            var id = await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 12));
            await _servicio.CancelarAlquilerAsync(new CancelacionAlquilerDto { AlquilerId = id, MotivoCancelacion = "Primera" });

            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.CancelarAlquilerAsync(new CancelacionAlquilerDto { AlquilerId = id, MotivoCancelacion = "Segunda" }));

            Assert.Equal("Primera", Assert.Single(_alquileres.Datos).MotivoCancelacion);
        }

        // ---- Historial ----

        [Fact]
        public async Task El_ingreso_del_periodo_solo_suma_los_reservados_y_por_moneda()
        {
            await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 8, 10));
            var cancelado = await _servicio.RegistrarAlquilerAsync(Alquiler(ProximoLunes, 10, 12));
            var enDolares = Alquiler(ProximoLunes, 12, 14);
            enDolares.Moneda = TiposMoneda.Dolares;
            enDolares.Monto = 40;
            await _servicio.RegistrarAlquilerAsync(enDolares);
            await _servicio.CancelarAlquilerAsync(new CancelacionAlquilerDto { AlquilerId = cancelado, MotivoCancelacion = "No vino" });

            var resultado = await _servicio.ObtenerHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto());

            Assert.Equal(3, resultado.Alquileres.Count);
            Assert.Equal(25000, resultado.TotalesPorMoneda.Single(t => t.Moneda == TiposMoneda.Colones).Total);
            Assert.Equal(40, resultado.TotalesPorMoneda.Single(t => t.Moneda == TiposMoneda.Dolares).Total);
        }

        // ---- Dobles ----

        private sealed class BitacoraFalsa : IBitacoraService
        {
            public List<(string Accion, string? Detalle)> Registros { get; } = new();

            public Task RegistrarAsync(string accion, string modulo, string? detalle = null)
            {
                Registros.Add((accion, detalle));
                return Task.CompletedTask;
            }

            public Task RegistrarDeUsuarioAsync(string? usuarioId, string nombreUsuario, string? rol,
                string accion, string modulo, string? detalle = null) => Task.CompletedTask;

            public Task<ResultadoPaginado<BitacoraAccionDto>> ObtenerBitacoraAsync(FiltrosBitacoraDto filtros) =>
                throw new NotImplementedException();
        }

        private sealed class RepositorioArrendatariosFalso : IArrendatariosRepository
        {
            public List<Arrendatario> Datos { get; } = new();

            public Task<Arrendatario?> ObtenerPorIdAsync(int id) =>
                Task.FromResult(Datos.FirstOrDefault(a => a.Id == id));

            public Task AgregarAsync(Arrendatario arrendatario) => throw new NotImplementedException();
            public Task<Arrendatario?> BuscarPorIdentificacionAsync(string identificacion) => throw new NotImplementedException();
            public Task<IReadOnlyList<Arrendatario>> BuscarActivosAsync(string texto, int maximo) => throw new NotImplementedException();
        }

        private sealed class RepositorioEspaciosFalso : IEspaciosRepository
        {
            public List<EspacioFisico> Espacios { get; } = new();
            public List<CaracteristicaEspacio> Caracteristicas { get; } = new();

            public Task<IReadOnlyList<EspacioFisico>> ObtenerEspaciosPorIdsAsync(IReadOnlyCollection<int> ids) =>
                Task.FromResult<IReadOnlyList<EspacioFisico>>(Espacios.Where(e => ids.Contains(e.Id)).ToList());

            public Task<IReadOnlyList<CaracteristicaEspacio>> ObtenerCaracteristicasPorIdsAsync(IReadOnlyCollection<int> ids) =>
                Task.FromResult<IReadOnlyList<CaracteristicaEspacio>>(Caracteristicas.Where(c => ids.Contains(c.Id)).ToList());

            public Task<IReadOnlyList<EspacioFisico>> ObtenerEspaciosAsync(bool soloActivos) => throw new NotImplementedException();
            public Task<bool> ExisteNombreEspacioAsync(string nombre) => throw new NotImplementedException();
            public Task AgregarEspacioAsync(EspacioFisico espacio) => throw new NotImplementedException();
            public Task<bool> ActualizarCapacidadEspacioAsync(int id, int? capacidad) => throw new NotImplementedException();
            public Task<bool> CambiarEstadoEspacioAsync(int id, bool estado) => throw new NotImplementedException();
            public Task<IReadOnlyList<CaracteristicaEspacio>> ObtenerCaracteristicasAsync(bool soloActivos) => throw new NotImplementedException();
            public Task<bool> ExisteNombreCaracteristicaAsync(string nombre) => throw new NotImplementedException();
            public Task AgregarCaracteristicaAsync(CaracteristicaEspacio caracteristica) => throw new NotImplementedException();
            public Task<bool> CambiarEstadoCaracteristicaAsync(int id, bool estado) => throw new NotImplementedException();
            public Task<bool> EspacioTieneAlquileresAsync(int id) => throw new NotImplementedException();
            public Task EliminarEspacioAsync(int id) => throw new NotImplementedException();
            public Task<bool> CaracteristicaTieneAlquileresAsync(int id) => throw new NotImplementedException();
            public Task EliminarCaracteristicaAsync(int id) => throw new NotImplementedException();

            // El configurado; por defecto el del arranque (L-V 8-20, S-D 8-17).
            public HorarioAlquiler Horario { get; set; } = HorarioAlquiler.PorDefecto();

            public Task<HorarioAlquiler> ObtenerHorarioAsync() => Task.FromResult(Horario);
            public Task GuardarHorarioAsync(HorarioAlquiler horario) => throw new NotImplementedException();
        }

        // Reproduce en memoria la consulta de choques del repositorio real, con la
        // misma regla de traslape (ReglasAlquiler.SeTraslapan).
        private sealed class RepositorioAlquileresFalso : IAlquileresRepository
        {
            public List<AlquilerEspacio> Datos { get; } = new();

            public Task AgregarAlquilerAsync(AlquilerEspacio alquiler)
            {
                alquiler.Id = Datos.Count + 1;
                Datos.Add(alquiler);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<AlquilerEspacio>> ObtenerChoquesAsync(
                DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin, IReadOnlyCollection<int> espacioIds)
            {
                var choques = Datos
                    .Where(a => a.Estado == EstadoAlquiler.Reservado
                                && a.Fecha == fecha.Date
                                && ReglasAlquiler.SeTraslapan(a.HoraInicio, a.HoraFin, horaInicio, horaFin)
                                && a.Espacios.Any(e => espacioIds.Contains(e.Id)))
                    .Select(a =>
                    {
                        // El repositorio real incluye al arrendatario para el mensaje.
                        a.Arrendatario ??= new Arrendatario { Id = a.ArrendatarioId, Nombre = a.ArrendatarioId == 1 ? "Ana Rojas" : "Grupo Scout" };
                        return a;
                    })
                    .ToList();

                return Task.FromResult<IReadOnlyList<AlquilerEspacio>>(choques);
            }

            public Task<AlquilerEspacio?> ObtenerPorIdAsync(int id) =>
                Task.FromResult(Datos.FirstOrDefault(a => a.Id == id));

            public Task<IReadOnlyList<AlquilerEspacio>> ObtenerHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros) =>
                Task.FromResult<IReadOnlyList<AlquilerEspacio>>(Datos.ToList());

            // El calendario paginado se prueba en AlquileresHistorialPaginadoTests.
            public Task<SIGAC.Application.DTOs.ResultadoPaginado<AlquilerEspacio>> ObtenerPaginaHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros) => throw new NotImplementedException();
            public Task<IReadOnlyList<SIGAC.Application.DTOs.MontoPorMonedaDto>> ObtenerTotalesPorMonedaAsync(FiltrosHistorialAlquilerDto filtros) => throw new NotImplementedException();

            public Task<bool> CancelarAsync(int id, string motivoCancelacion)
            {
                var alquiler = Datos.FirstOrDefault(a => a.Id == id && a.Estado == EstadoAlquiler.Reservado);
                if (alquiler is null)
                    return Task.FromResult(false);

                alquiler.Estado = EstadoAlquiler.Cancelado;
                alquiler.MotivoCancelacion = motivoCancelacion;
                return Task.FromResult(true);
            }

            // Lo usa el panorama de Reportes, no el servicio de alquileres.
            public Task<IReadOnlyList<SIGAC.Application.DTOs.Reportes.AlquilerPanoramaDto>> ObtenerParaPanoramaAsync(int mesesHaciaAtras) =>
                throw new NotImplementedException();
        }
    }
}
