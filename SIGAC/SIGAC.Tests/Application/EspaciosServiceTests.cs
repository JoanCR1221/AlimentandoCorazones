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
    // Eliminar sectores y características: solo lo que nunca se usó en un
    // alquiler. Con repositorios en memoria, mismo criterio que
    // ProyectosServiceTests.
    public class EspaciosServiceTests
    {
        private readonly RepositorioEspaciosFalso _repositorio = new();
        private readonly BitacoraFalsa _bitacora = new();
        private readonly EspaciosService _servicio;

        public EspaciosServiceTests()
        {
            _servicio = new EspaciosService(_repositorio, _bitacora);

            _repositorio.Espacios.Add(new EspacioFisico { Id = 1, Nombre = "Sala de servicio", Estado = true });
            _repositorio.Espacios.Add(new EspacioFisico { Id = 2, Nombre = "Terraza", Estado = true });
            _repositorio.Caracteristicas.Add(new CaracteristicaEspacio { Id = 1, Nombre = "Luz", Estado = true });
            _repositorio.Caracteristicas.Add(new CaracteristicaEspacio { Id = 2, Nombre = "Proyector", Estado = true });

            // La sala y la luz ya se usaron en un alquiler; la terraza y el
            // proyector no.
            _repositorio.EspaciosUsados.Add(1);
            _repositorio.CaracteristicasUsadas.Add(1);
        }

        [Fact]
        public async Task Un_sector_que_nunca_se_alquilo_se_elimina_y_queda_en_la_bitacora()
        {
            await _servicio.EliminarEspacioAsync(2);

            Assert.DoesNotContain(_repositorio.Espacios, e => e.Id == 2);
            var (accion, detalle) = Assert.Single(_bitacora.Registros);
            Assert.Equal(AccionesBitacora.Eliminar, accion);
            Assert.Contains("Terraza", detalle);
        }

        [Fact]
        public async Task Un_sector_ya_alquilado_no_se_elimina_y_se_sugiere_desactivarlo()
        {
            var ex = await Assert.ThrowsAsync<ValidationException>(() => _servicio.EliminarEspacioAsync(1));

            Assert.Contains("Desactívelo", ex.Message);
            Assert.Contains(_repositorio.Espacios, e => e.Id == 1);
            Assert.Empty(_bitacora.Registros);
        }

        [Fact]
        public async Task Una_caracteristica_que_nunca_se_pidio_se_elimina()
        {
            await _servicio.EliminarCaracteristicaAsync(2);

            Assert.DoesNotContain(_repositorio.Caracteristicas, c => c.Id == 2);
            Assert.Equal(AccionesBitacora.Eliminar, Assert.Single(_bitacora.Registros).Accion);
        }

        [Fact]
        public async Task Una_caracteristica_ya_pedida_no_se_elimina()
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.EliminarCaracteristicaAsync(1));

            Assert.Contains(_repositorio.Caracteristicas, c => c.Id == 1);
        }

        [Fact]
        public async Task Eliminar_algo_que_no_existe_avisa_que_no_existe()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _servicio.EliminarEspacioAsync(99));
            await Assert.ThrowsAsync<NotFoundException>(() => _servicio.EliminarCaracteristicaAsync(99));
        }

        // ---- Horario de alquiler ----

        private static HorarioAlquilerDto Horario(int aperturaLv, int cierreLv, int aperturaSd, int cierreSd) => new()
        {
            AperturaEntreSemana = new TimeSpan(aperturaLv, 0, 0),
            CierreEntreSemana = new TimeSpan(cierreLv, 0, 0),
            AperturaFinDeSemana = new TimeSpan(aperturaSd, 0, 0),
            CierreFinDeSemana = new TimeSpan(cierreSd, 0, 0)
        };

        [Fact]
        public async Task Guardar_el_horario_lo_cambia_y_lo_anota_en_la_bitacora()
        {
            await _servicio.ActualizarHorarioAsync(Horario(7, 22, 9, 18));

            var horario = await _servicio.ObtenerHorarioAsync();
            Assert.Equal(new TimeSpan(7, 0, 0), horario.AperturaEntreSemana);
            Assert.Equal(new TimeSpan(18, 0, 0), horario.CierreFinDeSemana);

            var (accion, detalle) = Assert.Single(_bitacora.Registros);
            Assert.Equal(AccionesBitacora.Editar, accion);
            Assert.Contains("9:00 a. m. – 6:00 p. m.", detalle);
        }

        [Fact]
        public async Task Un_cierre_antes_de_la_apertura_se_rechaza()
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.ActualizarHorarioAsync(Horario(8, 20, 17, 8)));

            Assert.Equal(HorarioAlquiler.PorDefecto().CierreFinDeSemana, _repositorio.Horario.CierreFinDeSemana);
        }

        [Fact]
        public async Task El_horario_exige_las_cuatro_horas()
        {
            var incompleto = Horario(8, 20, 8, 17);
            incompleto.CierreEntreSemana = null;

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.ActualizarHorarioAsync(incompleto));
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

        private sealed class RepositorioEspaciosFalso : IEspaciosRepository
        {
            public List<EspacioFisico> Espacios { get; } = new();
            public List<CaracteristicaEspacio> Caracteristicas { get; } = new();
            public HashSet<int> EspaciosUsados { get; } = new();
            public HashSet<int> CaracteristicasUsadas { get; } = new();

            public Task<IReadOnlyList<EspacioFisico>> ObtenerEspaciosPorIdsAsync(IReadOnlyCollection<int> ids) =>
                Task.FromResult<IReadOnlyList<EspacioFisico>>(Espacios.Where(e => ids.Contains(e.Id)).ToList());

            public Task<bool> EspacioTieneAlquileresAsync(int id) => Task.FromResult(EspaciosUsados.Contains(id));

            public Task EliminarEspacioAsync(int id)
            {
                Espacios.RemoveAll(e => e.Id == id);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<CaracteristicaEspacio>> ObtenerCaracteristicasPorIdsAsync(IReadOnlyCollection<int> ids) =>
                Task.FromResult<IReadOnlyList<CaracteristicaEspacio>>(Caracteristicas.Where(c => ids.Contains(c.Id)).ToList());

            public Task<bool> CaracteristicaTieneAlquileresAsync(int id) => Task.FromResult(CaracteristicasUsadas.Contains(id));

            public Task EliminarCaracteristicaAsync(int id)
            {
                Caracteristicas.RemoveAll(c => c.Id == id);
                return Task.CompletedTask;
            }

            public HorarioAlquiler Horario { get; set; } = HorarioAlquiler.PorDefecto();

            public Task<HorarioAlquiler> ObtenerHorarioAsync() => Task.FromResult(Horario);

            public Task GuardarHorarioAsync(HorarioAlquiler horario)
            {
                Horario = horario;
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<EspacioFisico>> ObtenerEspaciosAsync(bool soloActivos) => throw new NotImplementedException();
            public Task<bool> ExisteNombreEspacioAsync(string nombre) => throw new NotImplementedException();
            public Task AgregarEspacioAsync(EspacioFisico espacio) => throw new NotImplementedException();
            public Task<bool> ActualizarCapacidadEspacioAsync(int id, int? capacidad) => throw new NotImplementedException();
            public Task<bool> CambiarEstadoEspacioAsync(int id, bool estado) => throw new NotImplementedException();
            public Task<IReadOnlyList<CaracteristicaEspacio>> ObtenerCaracteristicasAsync(bool soloActivos) => throw new NotImplementedException();
            public Task<bool> ExisteNombreCaracteristicaAsync(string nombre) => throw new NotImplementedException();
            public Task AgregarCaracteristicaAsync(CaracteristicaEspacio caracteristica) => throw new NotImplementedException();
            public Task<bool> CambiarEstadoCaracteristicaAsync(int id, bool estado) => throw new NotImplementedException();
        }
    }
}
