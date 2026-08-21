using SIGAC.Application.DTOs.Beneficiarios;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    public class BeneficiariosService : IBeneficiariosService
    {
        private readonly IBeneficiariosRepository _repository;

        public BeneficiariosService(IBeneficiariosRepository repository)
        {
            _repository = repository;
        }

        public async Task RegistrarBeneficiarioAsync(BeneficiarioCrearDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Categoria))
                    throw new ValidationException("Nombre y Categoría son obligatorios.");

                if (dto.FechaNacimiento == default)
                    throw new ValidationException("La fecha de nacimiento es obligatoria.");

                if (dto.FechaNacimiento.Date > DateTime.Today)
                    throw new ValidationException("La fecha de nacimiento no puede ser futura.");

                var esOtroDocumento = dto.TipoDocumento == "Otro";
                if (esOtroDocumento && string.IsNullOrWhiteSpace(dto.TipoDocumentoOtro))
                    throw new ValidationException("Debe especificar el tipo de documento cuando selecciona 'Otro'.");

                var nombre = dto.Nombre.Trim();

                if (await _repository.ExisteAsync(nombre, dto.FechaNacimiento))
                    throw new DuplicateException("Ya existe un beneficiario con ese nombre y fecha de nacimiento.");

                var beneficiario = new Beneficiario
                {
                    Nombre = nombre,
                    FechaNacimiento = dto.FechaNacimiento,
                    Categoria = dto.Categoria,
                    Telefono = dto.Telefono,
                    Direccion = dto.Direccion,
                    Estado = true,
                    FechaRegistro = DateTime.Now,
                    TipoDocumento = dto.TipoDocumento,
                    NumIdentidad = dto.NumIdentidad,
                    // Solo se conserva la especificación cuando el tipo es "Otro".
                    TipoDocumentoOtro = esOtroDocumento ? dto.TipoDocumentoOtro!.Trim() : null
                };

                await _repository.AgregarAsync(beneficiario);
            }
            catch (Exception ex) when (ex is not ValidationException and not DuplicateException)
            {
                throw new Exception("Error al registrar el beneficiario.", ex);
            }
        }

        public async Task<BeneficiarioEditarDto?> ObtenerParaEditarAsync(int id)
        {
            try
            {
                var beneficiario = await _repository.ObtenerPorIdAsync(id);
                if (beneficiario is null)
                    return null;

                return new BeneficiarioEditarDto
                {
                    Nombre = beneficiario.Nombre,
                    FechaNacimiento = beneficiario.FechaNacimiento,
                    Categoria = beneficiario.Categoria,
                    Telefono = beneficiario.Telefono,
                    Direccion = beneficiario.Direccion
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el beneficiario.", ex);
            }
        }

        public async Task ActualizarBeneficiarioAsync(int id, BeneficiarioEditarDto dto)
        {
            try
            {
                var beneficiario = await _repository.ObtenerPorIdAsync(id)
                    ?? throw new NotFoundException("El beneficiario no existe.");

                if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Categoria))
                    throw new ValidationException("Nombre y Categoría son obligatorios.");

                if (dto.FechaNacimiento == default)
                    throw new ValidationException("La fecha de nacimiento es obligatoria.");

                if (dto.FechaNacimiento.Date > DateTime.Today)
                    throw new ValidationException("La fecha de nacimiento no puede ser futura.");

                beneficiario.Nombre = dto.Nombre.Trim();
                beneficiario.FechaNacimiento = dto.FechaNacimiento;
                beneficiario.Categoria = dto.Categoria;
                beneficiario.Telefono = dto.Telefono;
                beneficiario.Direccion = dto.Direccion;

                await _repository.ActualizarAsync(beneficiario);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al actualizar el beneficiario.", ex);
            }
        }

        public async Task<IEnumerable<BeneficiarioListaDto>> ObtenerBeneficiariosAsync(FiltrosBeneficiarioDto filtros)
        {
            try
            {
                var beneficiarios = await _repository.ObtenerTodosAsync(filtros);

                return beneficiarios.Select(b => new BeneficiarioListaDto
                {
                    Id = b.Id,
                    Nombre = b.Nombre,
                    FechaNacimiento = b.FechaNacimiento,
                    Categoria = b.Categoria,
                    Telefono = b.Telefono,
                    Estado = b.Estado,
                    TipoDocumento = b.TipoDocumento,
                    NumIdentidad = b.NumIdentidad
                    
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar beneficiarios.", ex);
            }
        }

        public Task ActivarBeneficiarioAsync(int id) => CambiarEstadoAsync(id, true);

        public Task DesactivarBeneficiarioAsync(int id) => CambiarEstadoAsync(id, false);

        private async Task CambiarEstadoAsync(int id, bool estado)
        {
            try
            {
                _ = await _repository.ObtenerPorIdAsync(id)
                    ?? throw new NotFoundException("El beneficiario no existe.");

                await _repository.CambiarEstadoAsync(id, estado);
            }
            catch (Exception ex) when (ex is not NotFoundException)
            {
                throw new Exception("Error al cambiar el estado del beneficiario.", ex);
            }
        }
    }
}