using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Beneficiarios;
using SIGAC.Application.DTOs.Bitacora;
using SIGAC.Application.DTOs.Proyectos;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // Ficha del proyecto: edición con la descripción real, detalle con la lista de
    // participantes y quitar un participante. Con repositorios en memoria, mismo
    // criterio que EstadoArticuloTests.
    public class ProyectosServiceTests
    {
        private readonly RepositorioProyectosFalso _proyectos = new();
        private readonly BitacoraFalsa _bitacora = new();
        private readonly ProyectosService _servicio;

        public ProyectosServiceTests()
        {
            _servicio = new ProyectosService(_proyectos, new RepositorioBeneficiariosFalso(), _bitacora);
        }

        private ProyectoComunitario AgregarProyecto(EstadoProyecto estado = EstadoProyecto.EnCurso)
        {
            var proyecto = new ProyectoComunitario
            {
                Id = _proyectos.Proyectos.Count + 1,
                Nombre = "Taller de cocina",
                Descripcion = "Clases de cocina saludable para familias.",
                FechaInicio = new DateTime(2026, 9, 1),
                FechaEstimadaFin = new DateTime(2026, 10, 1),
                Estado = estado,
                FechaFinalizacionReal = estado == EstadoProyecto.Finalizado ? new DateTime(2026, 9, 20) : null
            };
            _proyectos.Proyectos.Add(proyecto);
            return proyecto;
        }

        private static ParticipanteProyecto Externo(int id, string nombre, string? contacto = null) => new()
        {
            Id = id,
            EsBeneficiario = false,
            NombreExterno = nombre,
            ContactoExterno = contacto,
            FechaRegistro = new DateTime(2026, 9, 2)
        };

        private static ParticipanteProyecto DeBeneficiario(int id, bool activo) => new()
        {
            Id = id,
            EsBeneficiario = true,
            BeneficiarioId = 50 + id,
            Beneficiario = new Beneficiario
            {
                Id = 50 + id,
                PrimerNombre = "Ana",
                PrimerApellido = "Rojas",
                CodigoPaisTelefono = "506",
                Telefono = "88887777",
                Estado = activo
            },
            FechaRegistro = new DateTime(2026, 9, 3)
        };

        // ---- Obtener para editar ----

        [Fact]
        public async Task Obtener_para_editar_trae_la_descripcion()
        {
            var proyecto = AgregarProyecto();

            var datos = await _servicio.ObtenerParaEditarAsync(proyecto.Id);

            Assert.NotNull(datos);
            Assert.Equal("Clases de cocina saludable para familias.", datos!.Descripcion);
            Assert.Equal(EstadoProyecto.EnCurso, datos.Estado);
        }

        [Fact]
        public async Task Obtener_para_editar_un_proyecto_inexistente_devuelve_null()
        {
            Assert.Null(await _servicio.ObtenerParaEditarAsync(99));
        }

        // ---- Detalle ----

        [Fact]
        public async Task Detalle_lista_los_participantes_ordenados_por_nombre()
        {
            var proyecto = AgregarProyecto();
            proyecto.Participantes.Add(Externo(1, "Zoila Mora", "zoila@correo.com"));
            proyecto.Participantes.Add(DeBeneficiario(2, activo: false));

            var detalle = await _servicio.ObtenerDetalleAsync(proyecto.Id);

            Assert.NotNull(detalle);
            Assert.Collection(detalle!.Participantes,
                beneficiario =>
                {
                    Assert.Equal("Ana Rojas", beneficiario.Nombre);
                    Assert.True(beneficiario.EsBeneficiario);
                    Assert.Equal("+506 88887777", beneficiario.Contacto);
                    Assert.False(beneficiario.BeneficiarioActivo);
                },
                externo =>
                {
                    Assert.Equal("Zoila Mora", externo.Nombre);
                    Assert.False(externo.EsBeneficiario);
                    Assert.Equal("zoila@correo.com", externo.Contacto);
                });
        }

        [Theory]
        [InlineData(EstadoProyecto.Planificado, false)]
        [InlineData(EstadoProyecto.EnCurso, false)]
        [InlineData(EstadoProyecto.Finalizado, true)]
        [InlineData(EstadoProyecto.Cancelado, true)]
        public async Task Detalle_indica_si_el_proyecto_esta_cerrado(EstadoProyecto estado, bool cerrado)
        {
            var proyecto = AgregarProyecto(estado);

            var detalle = await _servicio.ObtenerDetalleAsync(proyecto.Id);

            Assert.Equal(cerrado, detalle!.EstaCerrado);
        }

        // ---- Quitar participante ----

        [Fact]
        public async Task Quitar_participante_lo_saca_del_proyecto_y_lo_anota_en_la_bitacora()
        {
            var proyecto = AgregarProyecto();
            proyecto.Participantes.Add(Externo(1, "Zoila Mora"));

            await _servicio.QuitarParticipanteAsync(proyecto.Id, 1);

            Assert.Empty(proyecto.Participantes);
            var (accion, detalle) = Assert.Single(_bitacora.Registros);
            Assert.Equal(AccionesBitacora.Eliminar, accion);
            Assert.Contains("Zoila Mora", detalle);
        }

        [Theory]
        [InlineData(EstadoProyecto.Finalizado)]
        [InlineData(EstadoProyecto.Cancelado)]
        public async Task No_se_quitan_participantes_de_un_proyecto_cerrado(EstadoProyecto estado)
        {
            var proyecto = AgregarProyecto(estado);
            proyecto.Participantes.Add(Externo(1, "Zoila Mora"));

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.QuitarParticipanteAsync(proyecto.Id, 1));

            Assert.Single(proyecto.Participantes);
        }

        [Fact]
        public async Task No_se_quita_un_participante_de_otro_proyecto()
        {
            var proyecto = AgregarProyecto();
            var otro = AgregarProyecto();
            otro.Participantes.Add(Externo(7, "Zoila Mora"));

            await Assert.ThrowsAsync<NotFoundException>(() => _servicio.QuitarParticipanteAsync(proyecto.Id, 7));

            Assert.Single(otro.Participantes);
        }

        [Fact]
        public async Task Quitar_de_un_proyecto_inexistente_avisa_que_no_existe()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _servicio.QuitarParticipanteAsync(99, 1));
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

        private sealed class RepositorioProyectosFalso : IProyectosRepository
        {
            public List<ProyectoComunitario> Proyectos { get; } = new();

            public Task<ProyectoComunitario?> ObtenerPorIdAsync(int id) =>
                Task.FromResult(Proyectos.FirstOrDefault(p => p.Id == id));

            public Task<ProyectoComunitario?> ObtenerConParticipantesAsync(int id) => ObtenerPorIdAsync(id);

            public Task<bool> QuitarParticipanteAsync(int proyectoId, int participanteId)
            {
                var proyecto = Proyectos.FirstOrDefault(p => p.Id == proyectoId);
                var quitados = proyecto?.Participantes.RemoveAll(p => p.Id == participanteId) ?? 0;
                return Task.FromResult(quitados > 0);
            }

            public Task AgregarAsync(ProyectoComunitario proyecto) => throw new NotImplementedException();
            public Task ActualizarAsync(ProyectoComunitario proyecto) => throw new NotImplementedException();
            public Task<IEnumerable<ProyectoComunitario>> ObtenerTodosAsync(FiltrosProyectoDto filtros) => throw new NotImplementedException();
            public Task FinalizarAsync(int id) => throw new NotImplementedException();
            public Task AgregarParticipanteAsync(ParticipanteProyecto participante) => throw new NotImplementedException();
            public Task<bool> ExisteParticipanteAsync(int proyectoId, int beneficiarioId) => throw new NotImplementedException();
        }

        // ProyectosService solo lo usa al agregar participantes, que estos tests no cubren.
        private sealed class RepositorioBeneficiariosFalso : IBeneficiariosRepository
        {
            public Task AgregarAsync(Beneficiario beneficiario) => throw new NotImplementedException();
            public Task ActualizarAsync(Beneficiario beneficiario) => throw new NotImplementedException();
            public Task<Beneficiario?> ObtenerPorIdAsync(int id) => throw new NotImplementedException();
            public Task<BeneficiarioCoincidente?> BuscarPorNombresYFechaAsync(string primerNombre, string segundoNombre, string primerApellido, string segundoApellido, DateTime fechaNacimiento, int? idExcluir = null) => throw new NotImplementedException();
            public Task<ResultadoPaginado<Beneficiario>> ObtenerPaginaAsync(FiltrosBeneficiarioDto filtros) => throw new NotImplementedException();
            public Task<BeneficiarioCoincidente?> BuscarPorNumIdentidadAsync(string? numIdentidad, int? idExcluir = null) => throw new NotImplementedException();
            public Task CambiarEstadoAsync(int id, bool estado) => throw new NotImplementedException();
            public Task<ResumenRegistrosDto> ObtenerResumenAsync() => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoPorCategoriaDto>> ObtenerConteoPorCategoriaAsync() => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoPorMesDto>> ObtenerAltasPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
        }
    }
}
