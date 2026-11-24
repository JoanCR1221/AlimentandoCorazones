namespace SIGAC.Domain
{
    // Reparte el nombre completo que devuelve Hacienda (una sola cadena, en
    // mayúsculas) en los cuatro campos del formulario.
    //
    // SUPUESTO: Hacienda entrega primero los nombres y al final los dos apellidos
    // ("MARIA DE LOS ANGELES PEREZ MORA"). Si resultara ser al revés, el único lugar
    // que cambia es este: el orden vive acá y no en la pantalla ni en el cliente HTTP.
    //
    // Es una ayuda para no teclear, no una verdad: la persona revisa los campos antes
    // de guardar. Por eso el reparto es el caso común y no intenta resolver lo ambiguo.
    public static class DivisorNombreCompleto
    {
        // Partículas de apellidos y nombres compuestos. Se pegan a la palabra que
        // sigue ("De la Cruz", "Del Valle") y van en minúscula salvo al inicio.
        private static readonly HashSet<string> Particulas = new(StringComparer.OrdinalIgnoreCase)
        {
            "de", "del", "la", "las", "los", "san", "santa", "van", "von", "da", "di"
        };

        public readonly record struct Partes(
            string PrimerNombre,
            string? SegundoNombre,
            string PrimerApellido,
            string? SegundoApellido);

        // Devuelve null si hay menos de dos palabras: con una sola no se sabe si es
        // nombre o apellido, y es mejor no rellenar nada que rellenar mal.
        //   4 palabras: nombre nombre apellido apellido
        //   3 palabras: nombre apellido apellido (sin segundo nombre)
        //   2 palabras: nombre apellido (sin segundo apellido)
        // Con más de 4, los dos últimos grupos son los apellidos (con sus partículas)
        // y lo que queda al principio son los nombres: el primero es el primer nombre
        // y el resto el segundo ("Maria" + "de los Ángeles").
        public static Partes? Dividir(string? nombreCompleto)
        {
            var palabras = TextoNormalizador.CompactarEspacios(nombreCompleto)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (palabras.Length < 2)
                return null;

            // Siempre queda al menos una palabra para el nombre.
            var fin = palabras.Length;
            var apellidos = new List<string>(2);

            while (apellidos.Count < 2 && fin > 1)
            {
                var inicio = fin - 1;

                // Las partículas que preceden pertenecen al apellido, pero sin
                // dejar el nombre vacío.
                while (inicio > 1 && Particulas.Contains(palabras[inicio - 1]))
                    inicio--;

                apellidos.Insert(0, Formatear(palabras[inicio..fin]));
                fin = inicio;
            }

            var nombres = palabras[..fin];

            return new Partes(
                Formatear(nombres[..1]),
                nombres.Length > 1 ? Formatear(nombres[1..]) : null,
                apellidos[0],
                apellidos.Count > 1 ? apellidos[1] : null);
        }

        // Para donantes y arrendatarios, que guardan el nombre en un solo campo: lo que
        // devuelve Hacienda para una persona física en mayúsculas pasa a "Maria de los
        // Angeles Perez Mora". No se usa para razones sociales ("S.A." perdería sus
        // mayúsculas): esas se dejan como las entrega Hacienda.
        public static string FormatearNombre(string? nombreCompleto)
        {
            var palabras = TextoNormalizador.CompactarEspacios(nombreCompleto)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return palabras.Length == 0 ? string.Empty : Formatear(palabras);
        }

        // "DE LA CRUZ" -> "De la Cruz": primera letra en mayúscula en cada palabra,
        // salvo las partículas, que quedan en minúscula si no abren el campo.
        private static string Formatear(IReadOnlyList<string> palabras)
        {
            var resultado = new string[palabras.Count];

            for (var i = 0; i < palabras.Count; i++)
            {
                var palabra = palabras[i].ToLowerInvariant();

                if (i > 0 && Particulas.Contains(palabra))
                {
                    resultado[i] = palabra;
                    continue;
                }

                resultado[i] = Capitalizar(palabra);
            }

            return string.Join(' ', resultado);
        }

        // Mayúscula al inicio de la palabra y después de guion o apóstrofe
        // ("sanchez-mora" -> "Sanchez-Mora", "d'avila" -> "D'Avila").
        private static string Capitalizar(string palabra)
        {
            var letras = palabra.ToCharArray();
            var inicio = true;

            for (var i = 0; i < letras.Length; i++)
            {
                if (inicio)
                    letras[i] = char.ToUpperInvariant(letras[i]);

                inicio = letras[i] is '-' or '\'' or '’';
            }

            return new string(letras);
        }
    }
}
