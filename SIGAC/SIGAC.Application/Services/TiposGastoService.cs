using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Validators;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    // Administración del catálogo de tipos de gasto. Mismo esquema que
    // GastosService: valida con TipoGastoValidator, arma la entidad, delega en el
    // repositorio y deja rastro en la bitácora del módulo Gastos.
    public class TiposGastoService : ITiposGastoService
    {
        private readonly ITiposGastoRepository _repository;
        private readonly IBitacoraService _bitacora;

        public TiposGastoService(ITiposGastoRepository repository, IBitacoraService bitacora)
        {
            _repository = repository;
            _bitacora = bitacora;
        }

        public async Task<IReadOnlyList<TipoGastoDto>> ObtenerTodosAsync()
        {
            try
            {
                var tipos = await _repository.ObtenerTodosAsync(soloActivos: false);
                return tipos.Select(ADto).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar los tipos de gasto.", ex);
            }
        }

        public async Task RegistrarAsync(TipoGastoGuardarDto dto)
        {
            try
            {
                var datos = TipoGastoValidator.Validar(dto);
                await VerificarNombreLibreAsync(datos.Nombre, idPropio: null);

                var tipo = new TipoGasto
                {
                    Nombre = datos.Nombre,
                    Activo = true,
                    GeneraInventario = datos.GeneraInventario,
                    CuentaContablePorDefecto = datos.CuentaContablePorDefecto
                };

                await _repository.AgregarAsync(tipo);

                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Gastos,
                    $"Tipo de gasto #{tipo.Id}: {tipo.Nombre}" + (tipo.GeneraInventario ? ", genera inventario" : string.Empty));
            }
            catch (Exception ex) when (ex is not ValidationException and not DuplicateException)
            {
                throw new Exception("Error al registrar el tipo de gasto.", ex);
            }
        }

        public async Task EditarAsync(int id, TipoGastoGuardarDto dto)
        {
            try
            {
                var tipo = await _repository.ObtenerPorIdAsync(id)
                    ?? throw new NotFoundException("El tipo de gasto no existe.");

                var datos = TipoGastoValidator.Validar(dto);
                await VerificarNombreLibreAsync(datos.Nombre, idPropio: id);

                tipo.Nombre = datos.Nombre;
                tipo.GeneraInventario = datos.GeneraInventario;
                tipo.CuentaContablePorDefecto = datos.CuentaContablePorDefecto;

                await _repository.ActualizarAsync(tipo);

                await _bitacora.RegistrarAsync(AccionesBitacora.Editar, ModulosSistema.Gastos,
                    $"Tipo de gasto #{tipo.Id}: {tipo.Nombre}" + (tipo.GeneraInventario ? ", genera inventario" : string.Empty));
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DuplicateException)
            {
                throw new Exception("Error al editar el tipo de gasto.", ex);
            }
        }

        public Task ActivarAsync(int id) => CambiarEstadoAsync(id, activo: true);

        public Task DesactivarAsync(int id) => CambiarEstadoAsync(id, activo: false);

        private async Task CambiarEstadoAsync(int id, bool activo)
        {
            try
            {
                var tipo = await _repository.ObtenerPorIdAsync(id)
                    ?? throw new NotFoundException("El tipo de gasto no existe.");

                await _repository.CambiarEstadoAsync(id, activo);

                await _bitacora.RegistrarAsync(activo ? AccionesBitacora.Activar : AccionesBitacora.Desactivar,
                    ModulosSistema.Gastos, $"Tipo de gasto #{tipo.Id}: {tipo.Nombre}");
            }
            catch (Exception ex) when (ex is not NotFoundException)
            {
                throw new Exception("Error al cambiar el estado del tipo de gasto.", ex);
            }
        }

        // Sin distinguir tildes ni mayúsculas: "Mantenimiento de vehiculo" y
        // "Mantenimiento de Vehículo" partirían en dos el mismo subtotal del
        // reporte. El índice único de la base compara con la collation de la
        // columna, que puede distinguir tildes; por eso el chequeo fino va acá.
        // Se comparan también los inactivos: reactivar es la salida, no duplicar.
        private async Task VerificarNombreLibreAsync(string nombre, int? idPropio)
        {
            var tipos = await _repository.ObtenerTodosAsync(soloActivos: false);
            var existente = tipos.FirstOrDefault(t => t.Id != idPropio && TextoNormalizador.SonEquivalentes(t.Nombre, nombre));

            if (existente is not null)
            {
                throw new DuplicateException(existente.Activo
                    ? $"Ya existe el tipo de gasto '{existente.Nombre}'."
                    : $"Ya existe el tipo de gasto '{existente.Nombre}', inactivo. Reactívelo en lugar de crear otro.");
            }
        }

        internal static TipoGastoDto ADto(TipoGasto tipo) => new()
        {
            Id = tipo.Id,
            Nombre = tipo.Nombre,
            Activo = tipo.Activo,
            GeneraInventario = tipo.GeneraInventario,
            CuentaContablePorDefecto = tipo.CuentaContablePorDefecto
        };
    }
}
