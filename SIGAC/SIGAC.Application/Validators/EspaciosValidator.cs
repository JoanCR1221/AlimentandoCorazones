using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    // Validación de los dos catálogos del módulo de alquileres: espacios físicos
    // (sectores) y características. Comparten las reglas del nombre, así que viven
    // juntos.
    public static class EspaciosValidator
    {
        // Mismo largo en EspaciosFisicos.Nombre y CaracteristicasEspacio.Nombre.
        public const int LongitudMaximaNombre = 100;

        // Tope de sentido común: evita que un cero de más ("5000") pase como
        // capacidad real del local.
        public const int CapacidadMaxima = 1000;

        public static string ValidarNombre(string? valor, string queEs)
        {
            var normalizado = TextoNormalizador.NormalizarNombre(valor);

            if (normalizado.Length == 0)
                throw new ValidationException($"El nombre {queEs} es obligatorio.");

            if (normalizado.Length > LongitudMaximaNombre)
                throw new ValidationException(
                    $"El nombre no puede superar los {LongitudMaximaNombre} caracteres.");

            return normalizado;
        }

        // Opcional: null significa que el espacio no tiene una capacidad de personas
        // que tenga sentido (baños, cocina).
        public static int? ValidarCapacidad(int? capacidad)
        {
            if (capacidad is null)
                return null;

            if (capacidad < 1 || capacidad > CapacidadMaxima)
                throw new ValidationException(
                    $"La capacidad debe estar entre 1 y {CapacidadMaxima} personas.");

            return capacidad;
        }
    }
}
