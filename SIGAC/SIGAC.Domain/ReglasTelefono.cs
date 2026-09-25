using System.Text;

namespace SIGAC.Domain
{
    // Teléfono con código de país, compartido por Beneficiario y Donante. Se guarda
    // en dos columnas: el código de país sin el "+" ("506", "1") y el número local
    // solo con dígitos. Así la pantalla de edición recupera el país sin tener que
    // adivinar dónde termina el prefijo, y la regla de Costa Rica es directa.
    //
    // Las reglas están acá y no en un validador para que el campo de la pantalla y
    // la validación del servidor salgan de la misma definición (mismo criterio que
    // ReglasBeneficiario con el número de identidad).
    public static class ReglasTelefono
    {
        public const string CodigoPaisCostaRica = "506";

        // Costa Rica: 8 dígitos exactos.
        public const int DigitosCostaRica = 8;

        // Resto de los países: no se valida el plan de numeración de cada uno, solo
        // el tope del estándar E.164 (código de país + número, 15 dígitos como
        // máximo) y un mínimo que descarta números evidentemente incompletos.
        public const int MaximoDigitosInternacional = 15;
        public const int MinimoDigitosLocal = 4;

        // Los códigos de país tienen de 1 a 3 dígitos; la lista de la pantalla usa
        // además algunos de 4 que incluyen el código de área ("1268", Antigua).
        public const int LongitudMaximaCodigoPais = 4;

        public static bool EsCodigoPaisValido(string? codigo) =>
            !string.IsNullOrEmpty(codigo)
            && codigo.Length <= LongitudMaximaCodigoPais
            && codigo.All(char.IsAsciiDigit)
            && codigo[0] != '0';

        public static bool EsCostaRica(string? codigo) => codigo == CodigoPaisCostaRica;

        public static int LongitudMaximaNumeroPara(string? codigo) =>
            EsCostaRica(codigo)
                ? DigitosCostaRica
                : MaximoDigitosInternacional - (EsCodigoPaisValido(codigo) ? codigo!.Length : 1);

        // Predicado único de formato. Espera el número sin espacios.
        public static bool TieneFormatoNumero(string? codigo, string? numero)
        {
            if (!EsCodigoPaisValido(codigo) || string.IsNullOrEmpty(numero) || !numero.All(char.IsAsciiDigit))
                return false;

            return EsCostaRica(codigo)
                ? numero.Length == DigitosCostaRica
                : numero.Length >= MinimoDigitosLocal && numero.Length <= LongitudMaximaNumeroPara(codigo);
        }

        // Qué se espera del número. Lo muestra la pantalla como ayuda y lo usa el
        // validador en el mensaje de error: un solo texto para las dos cosas.
        public static string DescribirNumero(string? codigo) =>
            EsCostaRica(codigo)
                ? $"{DigitosCostaRica} dígitos, sin guiones ni espacios."
                : $"Entre {MinimoDigitosLocal} y {LongitudMaximaNumeroPara(codigo)} dígitos, sin el código de país, guiones ni espacios.";

        // Descarta lo que no es dígito y recorta al máximo del país. Es para filtrar
        // lo que se escribe en la pantalla; el validador vuelve a comprobar todo.
        public static string FiltrarNumero(string? codigo, string? valor)
        {
            if (string.IsNullOrEmpty(valor))
                return string.Empty;

            var maximo = LongitudMaximaNumeroPara(codigo);
            var filtrado = new StringBuilder(maximo);

            foreach (var caracter in valor)
            {
                if (char.IsAsciiDigit(caracter))
                    filtrado.Append(caracter);

                if (filtrado.Length == maximo)
                    break;
            }

            return filtrado.ToString();
        }

        // Para mostrar en listados: "+506 88887777". Los teléfonos de donantes
        // anteriores al código de país quedaron sin él y se muestran tal cual.
        public static string? Formatear(string? codigo, string? numero)
        {
            if (string.IsNullOrWhiteSpace(numero))
                return null;

            return string.IsNullOrWhiteSpace(codigo) ? numero : $"+{codigo} {numero}";
        }
    }
}
