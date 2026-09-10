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
    }
}
