using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IDonantesRepository
    {
        Task AgregarAsync(Donante donante);
        Task<Donante?> ObtenerPorIdAsync(int id);
        Task ActualizarAsync(Donante donante);

        // Devuelve una sola página, ya filtrada y ordenada en SQL, junto con el
        // total de registros que cumplen los filtros (que la grilla necesita para
        // saber cuántas páginas hay). Nunca materializa la tabla entera.
        Task<ResultadoPaginado<Donante>> ObtenerTodosAsync(FiltrosDonanteDto filtros);

        // Sirve para AVISAR de un posible homónimo, no para bloquear el registro.
        //
        // Es la diferencia con ExisteNombreAsync de IInventarioRepository, que tiene
        // la misma firma pero otro significado: allá el nombre es la clave natural
        // del catálogo y está respaldado por UX_Articulos_Nombre, así que un true
        // impide guardar. Acá IX_Donantes_Nombre NO es único a propósito (dos
        // personas distintas pueden llamarse igual y Donante no tiene número de
        // documento con el cual desempatarlas), así que un true solo justifica
        // preguntarle al usuario si no está registrando dos veces al mismo.
        //
        // idExcluir permite editar un donante sin que se detecte a sí mismo.
        Task<bool> ExisteNombreAsync(string nombre, int? idExcluir = null);

        // Baja lógica: apaga o enciende Donante.Estado. Un solo método con el valor
        // como parámetro (y no ActivarAsync/DesactivarAsync separados) porque la
        // escritura es idéntica en los dos sentidos; el par de métodos con nombre
        // propio vive en el servicio, que es donde se lee la intención. Mismo
        // reparto que IBeneficiariosRepository.CambiarEstadoAsync.
        Task CambiarEstadoAsync(int id, bool estado);
    }
}
