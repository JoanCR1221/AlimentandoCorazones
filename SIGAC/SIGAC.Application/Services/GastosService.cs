using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Application.Validators;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    // Orquesta el alta, la edición y la anulación de gastos operativos: valida
    // con GastoOperativoValidator, arma la entidad y delega en el repositorio.
    // Las reglas de validación no viven acá.
    public class GastosService : IGastosService
    {
        private readonly IGastosRepository _repository;
        private readonly IBitacoraService _bitacora;

        public GastosService(IGastosRepository repository, IBitacoraService bitacora)
        {
            _repository = repository;
            _bitacora = bitacora;
        }

        public async Task<int> RegistrarGastoAsync(GastoOperativoCrearDto dto)
        {
            try
            {
                var datos = GastoOperativoValidator.Validar(dto);

                var gasto = new GastoOperativo
                {
                    Categoria = datos.Categoria,
                    Monto = datos.Monto,
                    Moneda = datos.Moneda,
                    Fecha = datos.Fecha,
                    Descripcion = datos.Descripcion,
                    Responsable = datos.Responsable,
                    Estado = EstadoGastoOperativo.Activo,
                    FechaRegistro = DateTime.Now
                };

                await _repository.AgregarAsync(gasto);

                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Gastos,
                    $"Gasto #{gasto.Id}: {gasto.Categoria}, {gasto.Monto:N2} {gasto.Moneda}, {gasto.Descripcion}");

                return gasto.Id;
            }
            catch (Exception ex) when (ex is not ValidationException)
            {
                throw new Exception("Error al registrar el gasto operativo.", ex);
            }
        }

        public async Task<GastoOperativoEditarDto?> ObtenerParaEditarAsync(int id)
        {
            try
            {
                var gasto = await _repository.ObtenerPorIdAsync(id);
                if (gasto is null)
                    return null;

                return new GastoOperativoEditarDto
                {
                    Categoria = gasto.Categoria,
                    Monto = gasto.Monto,
                    Moneda = gasto.Moneda,
                    Fecha = gasto.Fecha,
                    Descripcion = gasto.Descripcion,
                    Responsable = gasto.Responsable
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el gasto operativo.", ex);
            }
        }

        public async Task EditarGastoAsync(int id, GastoOperativoEditarDto dto)
        {
            try
            {
                var gasto = await _repository.ObtenerPorIdAsync(id)
                    ?? throw new NotFoundException("El gasto operativo no existe.");

                // Un gasto anulado es un registro cerrado: editarlo cambiaría el
                // respaldo contable de una anulación ya comunicada. Para corregirlo
                // hay que registrar uno nuevo, no reabrir el anulado.
                if (gasto.Estado == EstadoGastoOperativo.Anulado)
                    throw new ValidationException("No se puede editar un gasto operativo anulado.");

                var datos = GastoOperativoValidator.Validar(dto);

                gasto.Categoria = datos.Categoria;
                gasto.Monto = datos.Monto;
                gasto.Moneda = datos.Moneda;
                gasto.Fecha = datos.Fecha;
                gasto.Descripcion = datos.Descripcion;
                gasto.Responsable = datos.Responsable;

                await _repository.ActualizarAsync(gasto);

                await _bitacora.RegistrarAsync(AccionesBitacora.Editar, ModulosSistema.Gastos,
                    $"Gasto #{gasto.Id}: {gasto.Categoria}, {gasto.Monto:N2} {gasto.Moneda}, {gasto.Descripcion}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al editar el gasto operativo.", ex);
            }
        }

        public async Task<GastosConsultaDto> ObtenerGastosAsync(FiltrosGastoDto filtros)
        {
            try
            {
                var gastos = await _repository.ObtenerTodosAsync(filtros);

                var lista = gastos
                    .Select(g => new GastoOperativoListaDto
                    {
                        Id = g.Id,
                        Categoria = g.Categoria,
                        Monto = g.Monto,
                        Moneda = g.Moneda,
                        Fecha = g.Fecha,
                        Descripcion = g.Descripcion,
                        Responsable = g.Responsable,
                        Estado = g.Estado.ToString()
                    })
                    .ToList();

                // Un total por cada moneda presente, no un solo decimal: sumar
                // colones con dólares en un único número no representaría nada.
                // Excluye los anulados: un gasto anulado ya no representa dinero
                // efectivamente gastado, así que sumarlo distorsionaría el total del
                // período consultado.
                var totalesPorMoneda = lista
                    .Where(g => g.Estado == nameof(EstadoGastoOperativo.Activo))
                    .GroupBy(g => g.Moneda)
                    .Select(g => new MontoPorMonedaDto(g.Key, g.Sum(x => x.Monto)))
                    .ToList();

                return new GastosConsultaDto(lista, totalesPorMoneda);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar los gastos operativos.", ex);
            }
        }

        public async Task AnularGastoAsync(AnulacionGastoDto dto)
        {
            try
            {
                var gasto = await _repository.ObtenerPorIdAsync(dto.GastoId)
                    ?? throw new NotFoundException("El gasto operativo no existe.");

                if (gasto.Estado == EstadoGastoOperativo.Anulado)
                    throw new ValidationException("El gasto operativo ya está anulado.");

                var motivo = GastoOperativoValidator.ValidarMotivoAnulacion(dto.MotivoAnulacion);

                // Una sola llamada, y no anular acá y pedirle después al inventario
                // que revierta lo suyo: los dos pasos son todo-o-nada y cada uno con
                // su propia transacción no podía serlo. El repositorio resuelve
                // adentro cuántas entradas hay que revertir, incluido el caso de
                // ninguna: el enlace de AB#2501 es manual, así que un gasto de compra
                // sin entrada cargada es válido y no es un error.
                await _repository.AnularConEntradasVinculadasAsync(dto.GastoId, motivo);

                await _bitacora.RegistrarAsync(AccionesBitacora.Anular, ModulosSistema.Gastos,
                    $"Gasto #{gasto.Id}: {gasto.Descripcion}. Motivo: {motivo}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al anular el gasto operativo.", ex);
            }
        }
    }
}
