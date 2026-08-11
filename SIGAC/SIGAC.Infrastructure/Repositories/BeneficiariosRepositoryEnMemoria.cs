using System.Collections.Concurrent;
using SIGAC.Application.DTOs.Beneficiarios;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación TEMPORAL en memoria, solo para desarrollo/pruebas.
    // Reemplazar por una implementación con EF Core cuando se conecte la base de datos real.
    public class BeneficiariosRepositoryEnMemoria : IBeneficiariosRepository
    {
        private readonly ConcurrentDictionary<int, Beneficiario> _beneficiarios = new();
        private int _siguienteId = 1;

        public Task AgregarAsync(Beneficiario beneficiario)
        {
            beneficiario.Id = _siguienteId++;
            _beneficiarios[beneficiario.Id] = beneficiario;
            return Task.CompletedTask;
        }

        public Task ActualizarAsync(Beneficiario beneficiario)
        {
            _beneficiarios[beneficiario.Id] = beneficiario;
            return Task.CompletedTask;
        }

        public Task<Beneficiario?> ObtenerPorIdAsync(int id)
        {
            _beneficiarios.TryGetValue(id, out var beneficiario);
            return Task.FromResult(beneficiario);
        }

        public Task<bool> ExisteAsync(string nombre, DateTime fechaNacimiento)
        {
            var existe = _beneficiarios.Values.Any(b =>
                b.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase) &&
                b.FechaNacimiento == fechaNacimiento);
            return Task.FromResult(existe);
        }

        public Task<IEnumerable<Beneficiario>> ObtenerTodosAsync(FiltrosBeneficiarioDto filtros)
        {
            var query = _beneficiarios.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(filtros.Nombre))
                query = query.Where(b => b.Nombre.Contains(filtros.Nombre, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filtros.Categoria))
                query = query.Where(b => b.Categoria == filtros.Categoria);

            if (filtros.Estado.HasValue)
                query = query.Where(b => b.Estado == filtros.Estado.Value);

            return Task.FromResult(query);
        }

        public Task CambiarEstadoAsync(int id, bool estado)
        {
            if (_beneficiarios.TryGetValue(id, out var beneficiario))
                beneficiario.Estado = estado;
            return Task.CompletedTask;
        }
    }
}