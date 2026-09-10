using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
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

        public GastosService(IGastosRepository repository)
        {
            _repository = repository;
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
                    Fecha = datos.Fecha,
                    Descripcion = datos.Descripcion,
                    Responsable = datos.Responsable,
                    Estado = EstadoGastoOperativo.Activo,
                    FechaRegistro = DateTime.Now
                };

                await _repository.AgregarAsync(gasto);

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
                gasto.Fecha = datos.Fecha;
                gasto.Descripcion = datos.Descripcion;
                gasto.Responsable = datos.Responsable;

                await _repository.ActualizarAsync(gasto);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al editar el gasto operativo.", ex);
            }
        }
    }
}
