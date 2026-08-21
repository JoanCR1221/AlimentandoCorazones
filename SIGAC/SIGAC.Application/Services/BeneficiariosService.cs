using SIGAC.Application.DTOs.Beneficiarios;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
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
                var nombre = ValidarNombre(dto.Nombre, "El nombre", ReglasBeneficiario.LongitudMaximaNombre);
                var primerApellido = ValidarNombre(dto.PrimerApellido, "El primer apellido", ReglasBeneficiario.LongitudMaximaApellido);
                var segundoApellido = ValidarSegundoApellido(dto.SegundoApellido);
                var fechaNacimiento = ValidarFechaNacimiento(dto.FechaNacimiento);

                var esOtroDocumento = dto.TipoDocumento == "Otro";
                if (esOtroDocumento && string.IsNullOrWhiteSpace(dto.TipoDocumentoOtro))
                    throw new ValidationException("Debe especificar el tipo de documento cuando selecciona 'Otro'.");

                if (await _repository.ExisteAsync(nombre, primerApellido, segundoApellido, fechaNacimiento))
                    throw new DuplicateException("Ya existe un beneficiario con ese nombre, apellidos y fecha de nacimiento.");

                var beneficiario = new Beneficiario
                {
                    Nombre = nombre,
                    PrimerApellido = primerApellido,
                    SegundoApellido = segundoApellido,
                    FechaNacimiento = fechaNacimiento,
                    // La categoría se almacena, pero nunca se elige a mano.
                    Categoria = CategoriasBeneficiario.DerivarDesdeFechaNacimiento(fechaNacimiento),
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
                    PrimerApellido = beneficiario.PrimerApellido,
                    SegundoApellido = beneficiario.SegundoApellido,
                    FechaNacimiento = beneficiario.FechaNacimiento,
                    Telefono = beneficiario.Telefono,
                    Direccion = beneficiario.Direccion,
                    TipoDocumento = beneficiario.TipoDocumento,
                    NumIdentidad = beneficiario.NumIdentidad,
                    TipoDocumentoOtro = beneficiario.TipoDocumentoOtro
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

                var nombre = ValidarNombre(dto.Nombre, "El nombre", ReglasBeneficiario.LongitudMaximaNombre);
                var primerApellido = ValidarNombre(dto.PrimerApellido, "El primer apellido", ReglasBeneficiario.LongitudMaximaApellido);
                var segundoApellido = ValidarSegundoApellido(dto.SegundoApellido);
                var fechaNacimiento = ValidarFechaNacimiento(dto.FechaNacimiento);

                var esOtroDocumento = dto.TipoDocumento == "Otro";
                if (esOtroDocumento && string.IsNullOrWhiteSpace(dto.TipoDocumentoOtro))
                    throw new ValidationException("Debe especificar el tipo de documento cuando selecciona 'Otro'.");

                // Editar el nombre o la fecha puede chocar con otro beneficiario ya
                // registrado; se excluye el propio para no detectarse a sí mismo.
                if (await _repository.ExisteAsync(nombre, primerApellido, segundoApellido, fechaNacimiento, id))
                    throw new DuplicateException("Ya existe otro beneficiario con ese nombre, apellidos y fecha de nacimiento.");

                beneficiario.Nombre = nombre;
                beneficiario.PrimerApellido = primerApellido;
                beneficiario.SegundoApellido = segundoApellido;
                beneficiario.FechaNacimiento = fechaNacimiento;
                beneficiario.Categoria = CategoriasBeneficiario.DerivarDesdeFechaNacimiento(fechaNacimiento);
                beneficiario.Telefono = dto.Telefono;
                beneficiario.Direccion = dto.Direccion;
                beneficiario.TipoDocumento = dto.TipoDocumento;
                beneficiario.NumIdentidad = dto.NumIdentidad;
                // Solo se conserva la especificación cuando el tipo es "Otro".
                beneficiario.TipoDocumentoOtro = esOtroDocumento ? dto.TipoDocumentoOtro!.Trim() : null;

                await _repository.ActualizarAsync(beneficiario);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DuplicateException)
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
                    PrimerApellido = b.PrimerApellido,
                    SegundoApellido = b.SegundoApellido,
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

        // Devuelve el valor ya normalizado (recortado y sin espacios internos
        // repetidos), que es la forma en que se guarda.
        private static string ValidarNombre(string? valor, string etiqueta, int longitudMaxima)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            if (normalizado.Length == 0)
                throw new ValidationException($"{etiqueta} es obligatorio.");

            if (normalizado.Length < ReglasBeneficiario.LongitudMinimaNombre)
                throw new ValidationException($"{etiqueta} debe tener al menos {ReglasBeneficiario.LongitudMinimaNombre} caracteres.");

            if (normalizado.Length > longitudMaxima)
                throw new ValidationException($"{etiqueta} no puede superar los {longitudMaxima} caracteres.");

            if (!ReglasBeneficiario.TieneFormatoValido(normalizado))
                throw new ValidationException($"{etiqueta} solo puede contener letras, apóstrofes y guiones.");

            return normalizado;
        }

        private static string ValidarSegundoApellido(string? valor)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            // Opcional: cuando no se ingresa se guarda como cadena vacía, no como NULL.
            if (normalizado.Length == 0)
                return string.Empty;

            return ValidarNombre(normalizado, "El segundo apellido", ReglasBeneficiario.LongitudMaximaApellido);
        }

        private static DateTime ValidarFechaNacimiento(DateTime fechaNacimiento)
        {
            if (fechaNacimiento == default)
                throw new ValidationException("La fecha de nacimiento es obligatoria.");

            var fecha = fechaNacimiento.Date;

            if (fecha > DateTime.Today)
                throw new ValidationException("La fecha de nacimiento no puede ser futura.");

            if (CategoriasBeneficiario.CalcularEdad(fecha) > ReglasBeneficiario.EdadMaximaAnios)
                throw new ValidationException($"La fecha de nacimiento no es válida: supera los {ReglasBeneficiario.EdadMaximaAnios} años.");

            return fecha;
        }
    }
}
