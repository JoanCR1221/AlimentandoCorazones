using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Validators;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    // Configuración de los catálogos del módulo de alquileres. Eliminar solo se
    // permite mientras el sector o la característica no se haya usado en ningún
    // alquiler (uno cargado por error, por ejemplo). Si ya se usó, se desactiva:
    // borrarlo dejaría esos alquileres sin sector en el historial, y las FK de las
    // tablas intermedias (Restrict) lo impiden igual.
    public class EspaciosService : IEspaciosService
    {
        private readonly IEspaciosRepository _repository;
        private readonly IBitacoraService _bitacora;

        public EspaciosService(IEspaciosRepository repository, IBitacoraService bitacora)
        {
            _repository = repository;
            _bitacora = bitacora;
        }

        public async Task<IReadOnlyList<EspacioFisicoDto>> ObtenerEspaciosAsync(bool soloActivos)
        {
            try
            {
                var espacios = await _repository.ObtenerEspaciosAsync(soloActivos);

                return espacios
                    .Select(e => new EspacioFisicoDto
                    {
                        Id = e.Id,
                        Nombre = e.Nombre,
                        Capacidad = e.Capacidad,
                        Estado = e.Estado
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar los espacios.", ex);
            }
        }

        public async Task RegistrarEspacioAsync(EspacioFisicoCrearDto dto)
        {
            try
            {
                var nombre = EspaciosValidator.ValidarNombre(dto.Nombre, "del espacio");
                var capacidad = EspaciosValidator.ValidarCapacidad(dto.Capacidad);

                if (await _repository.ExisteNombreEspacioAsync(nombre))
                    throw new DuplicateException($"Ya existe un espacio llamado '{nombre}'.");

                var espacio = new EspacioFisico { Nombre = nombre, Capacidad = capacidad, Estado = true };
                await _repository.AgregarEspacioAsync(espacio);

                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Alquileres,
                    $"Espacio #{espacio.Id}: {espacio.Nombre}");
            }
            catch (Exception ex) when (ex is not ValidationException and not DuplicateException)
            {
                throw new Exception("Error al registrar el espacio.", ex);
            }
        }

        public async Task ActualizarCapacidadEspacioAsync(int id, int? capacidad)
        {
            try
            {
                var valor = EspaciosValidator.ValidarCapacidad(capacidad);

                if (!await _repository.ActualizarCapacidadEspacioAsync(id, valor))
                    throw new NotFoundException("El espacio no existe.");

                await _bitacora.RegistrarAsync(AccionesBitacora.Editar, ModulosSistema.Alquileres,
                    $"Espacio #{id}: capacidad {(valor?.ToString() ?? "sin definir")}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al actualizar la capacidad del espacio.", ex);
            }
        }

        public async Task CambiarEstadoEspacioAsync(int id, bool estado)
        {
            try
            {
                if (!await _repository.CambiarEstadoEspacioAsync(id, estado))
                    throw new NotFoundException("El espacio no existe.");

                await _bitacora.RegistrarAsync(
                    estado ? AccionesBitacora.Activar : AccionesBitacora.Desactivar,
                    ModulosSistema.Alquileres,
                    $"Espacio #{id}");
            }
            catch (Exception ex) when (ex is not NotFoundException)
            {
                throw new Exception("Error al cambiar el estado del espacio.", ex);
            }
        }

        public async Task EliminarEspacioAsync(int id)
        {
            try
            {
                var espacio = (await _repository.ObtenerEspaciosPorIdsAsync(new[] { id })).FirstOrDefault()
                    ?? throw new NotFoundException("El espacio no existe.");

                if (await _repository.EspacioTieneAlquileresAsync(id))
                    throw new ValidationException(
                        $"No se puede eliminar '{espacio.Nombre}': ya se usó en alquileres registrados y el historial " +
                        "lo necesita. Desactívelo para que deje de ofrecerse.");

                await _repository.EliminarEspacioAsync(id);

                await _bitacora.RegistrarAsync(AccionesBitacora.Eliminar, ModulosSistema.Alquileres,
                    $"Espacio #{id}: {espacio.Nombre}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al eliminar el espacio.", ex);
            }
        }

        public async Task<IReadOnlyList<CaracteristicaDto>> ObtenerCaracteristicasAsync(bool soloActivos)
        {
            try
            {
                var caracteristicas = await _repository.ObtenerCaracteristicasAsync(soloActivos);

                return caracteristicas
                    .Select(c => new CaracteristicaDto { Id = c.Id, Nombre = c.Nombre, Estado = c.Estado })
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar las características.", ex);
            }
        }

        public async Task RegistrarCaracteristicaAsync(CaracteristicaCrearDto dto)
        {
            try
            {
                var nombre = EspaciosValidator.ValidarNombre(dto.Nombre, "de la característica");

                if (await _repository.ExisteNombreCaracteristicaAsync(nombre))
                    throw new DuplicateException($"Ya existe una característica llamada '{nombre}'.");

                var caracteristica = new CaracteristicaEspacio { Nombre = nombre, Estado = true };
                await _repository.AgregarCaracteristicaAsync(caracteristica);

                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Alquileres,
                    $"Característica #{caracteristica.Id}: {caracteristica.Nombre}");
            }
            catch (Exception ex) when (ex is not ValidationException and not DuplicateException)
            {
                throw new Exception("Error al registrar la característica.", ex);
            }
        }

        public async Task CambiarEstadoCaracteristicaAsync(int id, bool estado)
        {
            try
            {
                if (!await _repository.CambiarEstadoCaracteristicaAsync(id, estado))
                    throw new NotFoundException("La característica no existe.");

                await _bitacora.RegistrarAsync(
                    estado ? AccionesBitacora.Activar : AccionesBitacora.Desactivar,
                    ModulosSistema.Alquileres,
                    $"Característica #{id}");
            }
            catch (Exception ex) when (ex is not NotFoundException)
            {
                throw new Exception("Error al cambiar el estado de la característica.", ex);
            }
        }

        public async Task EliminarCaracteristicaAsync(int id)
        {
            try
            {
                var caracteristica = (await _repository.ObtenerCaracteristicasPorIdsAsync(new[] { id })).FirstOrDefault()
                    ?? throw new NotFoundException("La característica no existe.");

                if (await _repository.CaracteristicaTieneAlquileresAsync(id))
                    throw new ValidationException(
                        $"No se puede eliminar '{caracteristica.Nombre}': ya se pidió en alquileres registrados y el " +
                        "historial la necesita. Desactívela para que deje de ofrecerse.");

                await _repository.EliminarCaracteristicaAsync(id);

                await _bitacora.RegistrarAsync(AccionesBitacora.Eliminar, ModulosSistema.Alquileres,
                    $"Característica #{id}: {caracteristica.Nombre}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al eliminar la característica.", ex);
            }
        }

        public async Task<HorarioAlquilerDto> ObtenerHorarioAsync()
        {
            try
            {
                var horario = await _repository.ObtenerHorarioAsync();

                return new HorarioAlquilerDto
                {
                    AperturaEntreSemana = horario.AperturaEntreSemana,
                    CierreEntreSemana = horario.CierreEntreSemana,
                    AperturaFinDeSemana = horario.AperturaFinDeSemana,
                    CierreFinDeSemana = horario.CierreFinDeSemana
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el horario de alquiler.", ex);
            }
        }

        public async Task ActualizarHorarioAsync(HorarioAlquilerDto dto)
        {
            try
            {
                var horario = AlquilerValidator.ValidarHorario(
                    dto.AperturaEntreSemana, dto.CierreEntreSemana,
                    dto.AperturaFinDeSemana, dto.CierreFinDeSemana);

                await _repository.GuardarHorarioAsync(horario);

                await _bitacora.RegistrarAsync(AccionesBitacora.Editar, ModulosSistema.Alquileres,
                    $"Horario de alquiler: L-V {ReglasAlquiler.FormatearFranja(horario.AperturaEntreSemana, horario.CierreEntreSemana)}; " +
                    $"S-D {ReglasAlquiler.FormatearFranja(horario.AperturaFinDeSemana, horario.CierreFinDeSemana)}");
            }
            catch (Exception ex) when (ex is not ValidationException)
            {
                throw new Exception("Error al guardar el horario de alquiler.", ex);
            }
        }
    }
}
