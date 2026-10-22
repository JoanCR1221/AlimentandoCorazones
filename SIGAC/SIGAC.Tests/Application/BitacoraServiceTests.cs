using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Bitacora;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // La bitácora es un efecto secundario de operaciones que ya quedaron guardadas: que
    // falle no puede convertir un éxito en un error (el usuario reintentaría y duplicaría
    // el registro).
    public class BitacoraServiceTests
    {
        [Fact]
        public async Task Si_el_repositorio_falla_registrar_no_propaga_el_error()
        {
            var servicio = new BitacoraService(new RepositorioBitacoraFalso { Falla = true }, new UsuarioActualFalso());

            await servicio.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Donaciones, "detalle");
        }

        [Fact]
        public async Task Si_no_se_puede_leer_el_usuario_actual_igual_se_registra_la_accion()
        {
            var repositorio = new RepositorioBitacoraFalso();
            var servicio = new BitacoraService(repositorio, new UsuarioActualFalso { Falla = true });

            await servicio.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Donaciones, "detalle");

            var registro = Assert.Single(repositorio.Acciones);
            Assert.Null(registro.UsuarioId);
        }

        [Fact]
        public async Task Con_sesion_registra_al_usuario_actual()
        {
            var repositorio = new RepositorioBitacoraFalso();
            var servicio = new BitacoraService(repositorio, new UsuarioActualFalso());

            await servicio.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Donaciones, "detalle");

            Assert.Equal("u1", Assert.Single(repositorio.Acciones).UsuarioId);
        }

        private sealed class UsuarioActualFalso : IUsuarioActual
        {
            public bool Falla { get; set; }

            public Task<DatosUsuarioActual?> ObtenerAsync()
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada al revalidar la sesión.");

                return Task.FromResult<DatosUsuarioActual?>(new DatosUsuarioActual("u1", "Ana", "ana@x.com", RolesSistema.Administrador));
            }
        }

        private sealed class RepositorioBitacoraFalso : IBitacoraRepository
        {
            public bool Falla { get; set; }
            public List<BitacoraAccion> Acciones { get; } = new();

            public Task RegistrarAccionAsync(BitacoraAccion accion)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                Acciones.Add(accion);
                return Task.CompletedTask;
            }

            public Task<ResultadoPaginado<BitacoraAccion>> ObtenerAccionesAsync(FiltrosBitacoraDto filtros) =>
                throw new NotImplementedException();
        }
    }
}
