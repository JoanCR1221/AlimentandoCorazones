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
        private readonly IBitacoraService _bitacora;

        public BeneficiariosService(IBeneficiariosRepository repository, IBitacoraService bitacora)
        {
            _repository = repository;
            _bitacora = bitacora;
        }

        public async Task RegistrarBeneficiarioAsync(BeneficiarioCrearDto dto)
        {
            try
            {
                var datos = BeneficiarioValidator.Validar(dto);

                var mismaPersona = await _repository.BuscarPorNombresYFechaAsync(
                    datos.PrimerNombre, datos.SegundoNombre,
                    datos.PrimerApellido, datos.SegundoApellido,
                    datos.FechaNacimiento);

                if (mismaPersona is not null)
                    throw Duplicado("Ya existe un beneficiario con esos nombres, apellidos y fecha de nacimiento", mismaPersona);

                // Segunda regla anti-duplicados, independiente de cómo se escriba el
                // nombre: el número de identidad identifica a la persona. Es global,
                // no depende del tipo de documento elegido. Los que no tienen
                // documento quedan fuera (NumIdentidad en null).
                var mismoDocumento = await _repository.BuscarPorNumIdentidadAsync(datos.NumIdentidad);

                if (mismoDocumento is not null)
                    throw Duplicado("Ya existe un beneficiario registrado con ese número de identidad", mismoDocumento);

                var beneficiario = new Beneficiario
                {
                    PrimerNombre = datos.PrimerNombre,
                    SegundoNombre = datos.SegundoNombre,
                    PrimerApellido = datos.PrimerApellido,
                    SegundoApellido = datos.SegundoApellido,
                    FechaNacimiento = datos.FechaNacimiento,
                    CodigoPaisTelefono = datos.CodigoPaisTelefono,
                    Telefono = datos.Telefono,
                    Direccion = datos.Direccion,
                    Estado = true,
                    FechaRegistro = DateTime.Now,
                    TipoDocumento = datos.TipoDocumento,
                    NumIdentidad = datos.NumIdentidad,
                    TipoDocumentoOtro = datos.TipoDocumentoOtro
                };

                await _repository.AgregarAsync(beneficiario);

                // Desde acá el alta ya está confirmada: si falla la bitácora no se
                // puede informar como "no se pudo registrar".
                try
                {
                    await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Beneficiarios,
                        $"Beneficiario #{beneficiario.Id}: {beneficiario.NombreCompleto}");
                }
                catch (Exception ex)
                {
                    throw new BitacoraNoRegistradaException(
                        "El beneficiario se registró, pero no se pudo anotar la acción en la bitácora.", ex);
                }
            }
            catch (Exception ex) when (ex is not ValidationException and not DuplicateException and not BitacoraNoRegistradaException)
            {
                throw new Exception("Error al registrar el beneficiario.", ex);
            }
        }

        // Dice cuál es el registro que ya existe y si está inactivo, para que no se
        // cargue a la misma persona dos veces sin saber que ya estaba.
        private static BeneficiarioDuplicadoException Duplicado(string motivo, BeneficiarioCoincidente existente) =>
            new(existente.Id, existente.Activo,
                $"{motivo}: {existente.NombreCompleto} (#{existente.Id}){(existente.Activo ? "" : ", que está inactivo")}.");

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
                    CodigoPaisTelefono = beneficiario.CodigoPaisTelefono,
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
                var mismaPersona = await _repository.BuscarPorNombresYFechaAsync(
                    datos.PrimerNombre, datos.SegundoNombre,
                    datos.PrimerApellido, datos.SegundoApellido,
                    datos.FechaNacimiento, id);

                if (mismaPersona is not null)
                    throw Duplicado("Ya existe otro beneficiario con esos nombres, apellidos y fecha de nacimiento", mismaPersona);

                // Se excluye el propio registro: editar sin tocar el documento no
                // debe detectarse a sí mismo como duplicado.
                var mismoDocumento = await _repository.BuscarPorNumIdentidadAsync(datos.NumIdentidad, id);

                if (mismoDocumento is not null)
                    throw Duplicado("Ya existe otro beneficiario registrado con ese número de identidad", mismoDocumento);

                beneficiario.PrimerNombre = datos.PrimerNombre;
                beneficiario.SegundoNombre = datos.SegundoNombre;
                beneficiario.PrimerApellido = datos.PrimerApellido;
                beneficiario.SegundoApellido = datos.SegundoApellido;
                beneficiario.FechaNacimiento = datos.FechaNacimiento;
                beneficiario.CodigoPaisTelefono = datos.CodigoPaisTelefono;
                beneficiario.Telefono = datos.Telefono;
                beneficiario.Direccion = datos.Direccion;
                beneficiario.TipoDocumento = datos.TipoDocumento;
                // Con "Sin documento" el validador ya devolvió null: al cambiar el tipo
                // el número anterior se borra en lugar de quedar huérfano.
                beneficiario.NumIdentidad = datos.NumIdentidad;
                beneficiario.TipoDocumentoOtro = datos.TipoDocumentoOtro;

                await _repository.ActualizarAsync(beneficiario);

                await _bitacora.RegistrarAsync(AccionesBitacora.Editar, ModulosSistema.Beneficiarios,
                    $"Beneficiario #{beneficiario.Id}: {beneficiario.NombreCompleto}");
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
                    CodigoPaisTelefono = b.CodigoPaisTelefono,
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

        public async Task<ResumenRegistrosDto> ObtenerResumenAsync()
        {
            try
            {
                return await _repository.ObtenerResumenAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el resumen de beneficiarios.", ex);
            }
        }

        public Task ActivarBeneficiarioAsync(int id) => CambiarEstadoAsync(id, true);

        public Task DesactivarBeneficiarioAsync(int id) => CambiarEstadoAsync(id, false);

        private async Task CambiarEstadoAsync(int id, bool estado)
        {
            try
            {
                var beneficiario = await _repository.ObtenerPorIdAsync(id)
                    ?? throw new NotFoundException("El beneficiario no existe.");

                await _repository.CambiarEstadoAsync(id, estado);

                await _bitacora.RegistrarAsync(
                    estado ? AccionesBitacora.Activar : AccionesBitacora.Desactivar,
                    ModulosSistema.Beneficiarios,
                    $"Beneficiario #{id}: {beneficiario.NombreCompleto}");
            }
            catch (Exception ex) when (ex is not NotFoundException)
            {
                throw new Exception("Error al cambiar el estado del beneficiario.", ex);
            }
        }
    }
}
