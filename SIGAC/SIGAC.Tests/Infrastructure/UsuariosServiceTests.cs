using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Bitacora;
using SIGAC.Application.DTOs.Seguridad;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Infrastructure.Data;
using SIGAC.Infrastructure.Identity;

namespace SIGAC.Tests.Infrastructure
{
    // La regla del último administrador activo: ninguna operación (cambiar el rol,
    // desactivar) puede dejar el sistema sin alguien que gestione usuarios. Se prueba
    // con ASP.NET Identity real sobre una base en memoria; el aislamiento Serializable
    // que protege la carrera entre dos administradores es de SQL Server y no se puede
    // verificar acá (la base en memoria ignora las transacciones).
    public class UsuariosServiceTests : IDisposable
    {
        private readonly ServiceProvider _proveedor;
        private readonly SigacDbContext _contexto;
        private readonly UserManager<UsuarioSigac> _usuarios;
        private readonly UsuarioActualFalso _actual = new();
        private readonly UsuariosService _servicio;

        public UsuariosServiceTests()
        {
            var servicios = new ServiceCollection();
            servicios.AddLogging();
            servicios.AddDbContext<SigacDbContext>(o => o
                .UseInMemoryDatabase($"usuarios-{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            servicios.AddIdentityCore<UsuarioSigac>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<SigacDbContext>();

            _proveedor = servicios.BuildServiceProvider();
            _contexto = _proveedor.GetRequiredService<SigacDbContext>();
            _usuarios = _proveedor.GetRequiredService<UserManager<UsuarioSigac>>();

            var roles = _proveedor.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var rol in RolesSistema.Todos)
                roles.CreateAsync(new IdentityRole(rol)).GetAwaiter().GetResult();

            _servicio = new UsuariosService(_usuarios, _actual, new PermisosFalso(), new BitacoraFalsa(), _contexto);
        }

        public void Dispose() => _proveedor.Dispose();

        private async Task<UsuarioSigac> CrearAsync(string nombre, string rol, bool activo = true)
        {
            var usuario = new UsuarioSigac
            {
                UserName = $"{nombre}@x.com",
                Email = $"{nombre}@x.com",
                Nombre = nombre,
                Estado = activo
            };

            Assert.True((await _usuarios.CreateAsync(usuario, "Clave#2026a")).Succeeded);
            Assert.True((await _usuarios.AddToRoleAsync(usuario, rol)).Succeeded);
            return usuario;
        }

        [Fact]
        public async Task No_se_puede_desactivar_al_unico_administrador_activo()
        {
            var unico = await CrearAsync("ana", RolesSistema.Administrador);
            _actual.Id = "otra-persona";

            await Assert.ThrowsAsync<UltimoAdministradorException>(() => _servicio.DesactivarUsuarioAsync(unico.Id));

            Assert.True((await _usuarios.FindByIdAsync(unico.Id))!.Estado);
        }

        [Fact]
        public async Task No_se_puede_quitar_el_rol_al_unico_administrador_activo()
        {
            var unico = await CrearAsync("ana", RolesSistema.Administrador);
            _actual.Id = "otra-persona";

            await Assert.ThrowsAsync<UltimoAdministradorException>(() => _servicio.CambiarRolAsync(
                new CambioRolDto { UsuarioId = unico.Id, NuevoRol = RolesSistema.Colaborador }));

            Assert.True(await _usuarios.IsInRoleAsync(unico, RolesSistema.Administrador));
        }

        [Fact]
        public async Task Un_administrador_desactivado_no_cuenta_como_el_otro_administrador()
        {
            var activo = await CrearAsync("ana", RolesSistema.Administrador);
            await CrearAsync("beto", RolesSistema.Administrador, activo: false);
            _actual.Id = "otra-persona";

            await Assert.ThrowsAsync<UltimoAdministradorException>(() => _servicio.DesactivarUsuarioAsync(activo.Id));
        }

        [Fact]
        public async Task Con_otro_administrador_activo_se_puede_desactivar_a_uno()
        {
            var ana = await CrearAsync("ana", RolesSistema.Administrador);
            await CrearAsync("beto", RolesSistema.Administrador);
            _actual.Id = "otra-persona";

            await _servicio.DesactivarUsuarioAsync(ana.Id);

            Assert.False((await _usuarios.FindByIdAsync(ana.Id))!.Estado);
        }

        // El camino feliz de CambiarRolAsync no se prueba acá: borra las revocaciones con
        // ExecuteDelete, que la base en memoria no soporta.

        [Fact]
        public async Task Desactivar_a_quien_no_es_administrador_no_exige_otro_administrador()
        {
            await CrearAsync("ana", RolesSistema.Administrador);
            var colaborador = await CrearAsync("carla", RolesSistema.Colaborador);
            _actual.Id = "otra-persona";

            await _servicio.DesactivarUsuarioAsync(colaborador.Id);

            Assert.False((await _usuarios.FindByIdAsync(colaborador.Id))!.Estado);
        }

        [Fact]
        public async Task Nadie_puede_desactivarse_a_si_mismo()
        {
            var ana = await CrearAsync("ana", RolesSistema.Administrador);
            await CrearAsync("beto", RolesSistema.Administrador);
            _actual.Id = ana.Id;

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.DesactivarUsuarioAsync(ana.Id));

            Assert.True((await _usuarios.FindByIdAsync(ana.Id))!.Estado);
        }

        [Fact]
        public async Task Nadie_puede_cambiarse_el_propio_rol()
        {
            var ana = await CrearAsync("ana", RolesSistema.Administrador);
            await CrearAsync("beto", RolesSistema.Administrador);
            _actual.Id = ana.Id;

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.CambiarRolAsync(
                new CambioRolDto { UsuarioId = ana.Id, NuevoRol = RolesSistema.Colaborador }));
        }

        private sealed class UsuarioActualFalso : IUsuarioActual
        {
            public string? Id { get; set; }

            public Task<DatosUsuarioActual?> ObtenerAsync() =>
                Task.FromResult<DatosUsuarioActual?>(Id is null ? null : new DatosUsuarioActual(Id, "Quien opera", "op@x.com", RolesSistema.Administrador));
        }

        private sealed class PermisosFalso : IPermisosRepository
        {
            public Task<IReadOnlyList<string>> ObtenerRevocadosAsync(string usuarioId) =>
                Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            public Task ReemplazarRevocadosAsync(string usuarioId, IEnumerable<string> permisos) => Task.CompletedTask;
        }

        private sealed class BitacoraFalsa : IBitacoraService
        {
            public Task RegistrarAsync(string accion, string modulo, string? detalle = null) => Task.CompletedTask;

            public Task RegistrarDeUsuarioAsync(string? usuarioId, string nombreUsuario, string? rol,
                string accion, string modulo, string? detalle = null) => Task.CompletedTask;

            public Task<ResultadoPaginado<BitacoraAccionDto>> ObtenerBitacoraAsync(FiltrosBitacoraDto filtros) =>
                throw new NotImplementedException();
        }
    }
}
