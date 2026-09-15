namespace SIGAC.Application.Interfaces
{
    // Revocaciones de permisos por usuario (tabla PermisosRevocados). Solo se
    // guardan las claves que el administrador apagó; los permisos efectivos los
    // calcula PermisosPorRol.CalcularEfectivos a partir del rol y de esta lista.
    public interface IPermisosRepository
    {
        Task<IReadOnlyList<string>> ObtenerRevocadosAsync(string usuarioId);

        // Reemplaza el conjunto completo: lo que no venga en la lista deja de
        // estar revocado. Es más simple y menos propenso a errores que agregar y
        // quitar de a uno desde un panel de switches.
        Task ReemplazarRevocadosAsync(string usuarioId, IEnumerable<string> permisos);
    }
}
