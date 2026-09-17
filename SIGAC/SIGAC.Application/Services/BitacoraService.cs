using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Bitacora;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    public class BitacoraService : IBitacoraService
    {
        // Largos de las columnas de Bitacora (ver SigacDbContext). Se recorta acá
        // en vez de dejar que SQL Server rechace la fila: un Detalle largo no debe
        // impedir que la acción quede registrada.
        private const int LongitudMaximaNombreUsuario = 256;
        private const int LongitudMaximaDetalle = 500;

        private const string NombreSinSesion = "(sin sesión)";

        private readonly IBitacoraRepository _repository;
        private readonly IUsuarioActual _usuarioActual;

        public BitacoraService(IBitacoraRepository repository, IUsuarioActual usuarioActual)
        {
            _repository = repository;
            _usuarioActual = usuarioActual;
        }

        public async Task RegistrarAsync(string accion, string modulo, string? detalle = null)
        {
            var actual = await _usuarioActual.ObtenerAsync();

            await RegistrarDeUsuarioAsync(
                actual?.Id,
                actual?.Nombre ?? NombreSinSesion,
                actual?.Rol,
                accion,
                modulo,
                detalle);
        }

        public async Task RegistrarDeUsuarioAsync(
            string? usuarioId, string nombreUsuario, string? rol,
            string accion, string modulo, string? detalle = null)
        {
            try
            {
                // Los CHECK de la base rechazarían igual un valor fuera del catálogo,
                // pero fallar acá dice cuál fue en vez de un error de SQL sin traducir.
                if (!AccionesBitacora.EsValida(accion))
                    throw new ArgumentException($"La acción de bitácora '{accion}' no existe en AccionesBitacora.", nameof(accion));

                if (!ModulosSistema.EsValido(modulo))
                    throw new ArgumentException($"El módulo de bitácora '{modulo}' no existe en ModulosSistema.", nameof(modulo));

                if (rol is not null && !RolesSistema.EsValido(rol))
                    rol = null;

                await _repository.RegistrarAccionAsync(new BitacoraAccion
                {
                    UsuarioId = string.IsNullOrWhiteSpace(usuarioId) ? null : usuarioId,
                    NombreUsuario = Recortar(string.IsNullOrWhiteSpace(nombreUsuario) ? NombreSinSesion : nombreUsuario, LongitudMaximaNombreUsuario)!,
                    Rol = rol,
                    Accion = accion,
                    Modulo = modulo,
                    Detalle = Recortar(detalle, LongitudMaximaDetalle),
                    Fecha = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Error al registrar la acción en la bitácora.", ex);
            }
        }

        public async Task<ResultadoPaginado<BitacoraAccionDto>> ObtenerBitacoraAsync(FiltrosBitacoraDto filtros)
        {
            try
            {
                var pagina = await _repository.ObtenerAccionesAsync(filtros);

                var elementos = pagina.Elementos.Select(a => new BitacoraAccionDto
                {
                    Id = a.Id,
                    UsuarioId = a.UsuarioId,
                    NombreUsuario = a.NombreUsuario,
                    Rol = a.Rol,
                    Accion = a.Accion,
                    Modulo = a.Modulo,
                    Detalle = a.Detalle,
                    Fecha = a.Fecha
                }).ToList();

                return new ResultadoPaginado<BitacoraAccionDto>(elementos, pagina.TotalRegistros);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar la bitácora.", ex);
            }
        }

        private static string? Recortar(string? valor, int maximo)
        {
            if (valor is null)
                return null;

            var compacto = valor.Trim();

            return compacto.Length <= maximo ? compacto : compacto[..(maximo - 1)] + "…";
        }
    }
}
