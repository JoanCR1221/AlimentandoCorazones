using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Beneficiarios;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Validators;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    // Orquesta el alta y la edición: valida con BeneficiarioValidator, arma la
    // entidad y delega en el repositorio. Las reglas de validación no viven acá.
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
                var datos = BeneficiarioValidator.Validar(dto);

                if (await _repository.ExisteAsync(
                        datos.PrimerNombre, datos.SegundoNombre,
                        datos.PrimerApellido, datos.SegundoApellido,
                        datos.FechaNacimiento))
                {
                    throw new DuplicateException("Ya existe un beneficiario con esos nombres, apellidos y fecha de nacimiento.");
                }

                // Segunda regla anti-duplicados, independiente de cómo se escriba el
                // nombre: el documento identifica a la persona. Los que no tienen
                // documento quedan fuera (NumIdentidad en null).
                if (await _repository.ExisteDocumentoAsync(datos.TipoDocumento, datos.NumIdentidad))
                {
                    throw new DuplicateException(
                        $"Ya existe un beneficiario registrado con ese documento: {DescribirDocumento(datos)} {datos.NumIdentidad}.");
                }

                var beneficiario = new Beneficiario
                {
                    PrimerNombre = datos.PrimerNombre,
                    SegundoNombre = datos.SegundoNombre,
                    PrimerApellido = datos.PrimerApellido,
                    SegundoApellido = datos.SegundoApellido,
                    FechaNacimiento = datos.FechaNacimiento,
                    // La categoría se almacena, pero nunca se elige a mano.
                    Categoria = CategoriasBeneficiario.DerivarDesdeFechaNacimiento(datos.FechaNacimiento),
                    Telefono = datos.Telefono,
                    Direccion = datos.Direccion,
                    Estado = true,
                    FechaRegistro = DateTime.Now,
                    TipoDocumento = datos.TipoDocumento,
                    NumIdentidad = datos.NumIdentidad,
                    TipoDocumentoOtro = datos.TipoDocumentoOtro
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
                    PrimerNombre = beneficiario.PrimerNombre,
                    SegundoNombre = beneficiario.SegundoNombre,
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

                var datos = BeneficiarioValidator.Validar(dto);

                // Editar los nombres o la fecha puede chocar con otro beneficiario ya
                // registrado; se excluye el propio para no detectarse a sí mismo.
                if (await _repository.ExisteAsync(
                        datos.PrimerNombre, datos.SegundoNombre,
                        datos.PrimerApellido, datos.SegundoApellido,
                        datos.FechaNacimiento, id))
                {
                    throw new DuplicateException("Ya existe otro beneficiario con esos nombres, apellidos y fecha de nacimiento.");
                }

                // Se excluye el propio registro: editar sin tocar el documento no
                // debe detectarse a sí mismo como duplicado.
                if (await _repository.ExisteDocumentoAsync(datos.TipoDocumento, datos.NumIdentidad, id))
                {
                    throw new DuplicateException(
                        $"Ya existe otro beneficiario registrado con ese documento: {DescribirDocumento(datos)} {datos.NumIdentidad}.");
                }

                beneficiario.PrimerNombre = datos.PrimerNombre;
                beneficiario.SegundoNombre = datos.SegundoNombre;
                beneficiario.PrimerApellido = datos.PrimerApellido;
                beneficiario.SegundoApellido = datos.SegundoApellido;
                beneficiario.FechaNacimiento = datos.FechaNacimiento;
                beneficiario.Categoria = CategoriasBeneficiario.DerivarDesdeFechaNacimiento(datos.FechaNacimiento);
                beneficiario.Telefono = datos.Telefono;
                beneficiario.Direccion = datos.Direccion;
                beneficiario.TipoDocumento = datos.TipoDocumento;
                // Con "Sin documento" el validador ya devolvió null: al cambiar el tipo
                // el número anterior se borra en lugar de quedar huérfano.
                beneficiario.NumIdentidad = datos.NumIdentidad;
                beneficiario.TipoDocumentoOtro = datos.TipoDocumentoOtro;

                await _repository.ActualizarAsync(beneficiario);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DuplicateException)
            {
                throw new Exception("Error al actualizar el beneficiario.", ex);
            }
        }

        public async Task<ResultadoPaginado<BeneficiarioListaDto>> ObtenerBeneficiariosAsync(FiltrosBeneficiarioDto filtros)
        {
            try
            {
                // El repositorio filtra, ordena y pagina en SQL: acá solo llegan los
                // registros de la página pedida, más el total para el paginador.
                var pagina = await _repository.ObtenerPaginaAsync(filtros);

                var elementos = pagina.Elementos.Select(b => new BeneficiarioListaDto
                {
                    Id = b.Id,
                    PrimerNombre = b.PrimerNombre,
                    SegundoNombre = b.SegundoNombre,
                    PrimerApellido = b.PrimerApellido,
                    SegundoApellido = b.SegundoApellido,
                    FechaNacimiento = b.FechaNacimiento,
                    Categoria = b.Categoria,
                    Telefono = b.Telefono,
                    Estado = b.Estado,
                    TipoDocumento = b.TipoDocumento,
                    NumIdentidad = b.NumIdentidad,
                    TipoDocumentoOtro = b.TipoDocumentoOtro

                }).ToList();

                return new ResultadoPaginado<BeneficiarioListaDto>(elementos, pagina.TotalRegistros);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar beneficiarios.", ex);
            }
        }

        // "Otro" por sí solo no identifica el documento en el mensaje de error: se
        // usa cómo se llama, que es lo que el usuario escribió.
        private static string DescribirDocumento(BeneficiarioValidado datos) =>
            TiposDocumento.RequiereNombreDelDocumento(datos.TipoDocumento) && !string.IsNullOrWhiteSpace(datos.TipoDocumentoOtro)
                ? datos.TipoDocumentoOtro
                : datos.TipoDocumento;

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
