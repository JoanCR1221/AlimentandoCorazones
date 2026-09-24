using System.Text;
using System.Text.RegularExpressions;

namespace SIGAC.Domain
{
    // Límites y formato de los datos personales de un beneficiario. Las longitudes
    // se comparten con el mapeo de EF Core para que no se desincronicen.
    public static class ReglasBeneficiario
    {
        public const int LongitudMinimaNombre = 2;
        public const int LongitudMaximaNombre = 100;
        public const int LongitudMaximaApellido = 75;
        public const int EdadMaximaAnios = 120;

        // Documentos de identidad. La cédula nacional y el DIMEX son puramente
        // numéricos y de longitud fija; el pasaporte y "Otro" son alfanuméricos.
        public const int DigitosCedulaNacional = 9;
        public const int DigitosMinimosDimex = 11;
        public const int DigitosMaximosDimex = 12;
        public const int LongitudMaximaNumIdentidad = 30;
        public const int LongitudMaximaTipoDocumentoOtro = 100;

        // El teléfono (con código de país) tiene sus propias reglas en
        // ReglasTelefono, compartidas con Donante.

        public const int LongitudMaximaDireccion = 200;

        // Letras (con tildes y ñ), apóstrofes, guiones y un espacio entre partes:
        // aparecen en apellidos reales (D'Ávila, Sánchez-Mora, De la Cruz).
        // Se rechazan dígitos y cualquier otro símbolo.
        //
        // Solo las letras que caben en las columnas: son varchar con la collation
        // SQL_Latin1_General_CP1 (página de códigos 1252), y una letra fuera de ella
        // ("ł", "ễ") se guardaría como "?". De ahí la lista explícita en vez de
        // \p{L}: el latín básico, el bloque Latin-1 (À-ÿ sin × ni ÷) y las letras
        // que la 1252 agrega (Š, š, Œ, œ, Ž, ž, Ÿ). Espera texto en forma NFC
        // (TextoNormalizador.NormalizarNombre), así "é" llega como una sola letra.
        private const string LetraGuardable = "A-Za-zÀ-ÖØ-öø-ÿŠšŒœŽžŸ";

        private static readonly Regex FormatoNombre = new(
            $@"^[{LetraGuardable}]+(?:[ '’\-][{LetraGuardable}]+)*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Solo dígitos ASCII: nada de guiones, espacios ni separadores.
        private static readonly Regex SoloDigitos = new(
            @"^[0-9]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Números, letras y guiones: el guion aparece en documentos reales de otros
        // países y en documentos internos ("CN-2026-014"). No se admiten espacios
        // porque TextoNormalizador.NormalizarNumeroDocumento ya los saca.
        private static readonly Regex AlfanumericoConGuiones = new(
            @"^[A-Za-z0-9\-]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Espera un valor ya compactado con TextoNormalizador.CompactarEspacios.
        public static bool TieneFormatoValido(string? valor) =>
            !string.IsNullOrWhiteSpace(valor) && FormatoNombre.IsMatch(valor);

        public static bool EsAlfanumericoConGuiones(string? valor) =>
            !string.IsNullOrWhiteSpace(valor) && AlfanumericoConGuiones.IsMatch(valor);

        private static bool TieneSoloDigitos(string? valor) =>
            !string.IsNullOrWhiteSpace(valor) && SoloDigitos.IsMatch(valor);

        public static bool TieneFormatoCedulaNacional(string? valor) =>
            TieneSoloDigitos(valor) && valor!.Length == DigitosCedulaNacional;

        public static bool TieneFormatoDimex(string? valor) =>
            TieneSoloDigitos(valor)
            && valor!.Length >= DigitosMinimosDimex
            && valor.Length <= DigitosMaximosDimex;

        // ---- Reglas del número de identidad según el tipo de documento ----
        //
        // Están acá y no dentro del validador para que la UI las consulte: el campo
        // de la pantalla y la validación del servidor salen de la misma definición
        // y no se pueden desincronizar. La UI es comodidad; el validador sigue
        // siendo el que manda, porque la pantalla se puede saltear.

        // Tope de caracteres del número. Cédula y DIMEX tienen longitud conocida;
        // pasaporte y "Otro" varían y usan el máximo general.
        public static int LongitudMaximaNumIdentidadPara(string? tipoDocumento) => tipoDocumento switch
        {
            TiposDocumento.CedulaNacional => DigitosCedulaNacional,
            TiposDocumento.Dimex => DigitosMaximosDimex,
            _ => LongitudMaximaNumIdentidad
        };

        // Cédula y DIMEX son puramente numéricos; pasaporte y "Otro", alfanuméricos.
        public static bool NumIdentidadEsSoloDigitos(string? tipoDocumento) =>
            tipoDocumento is TiposDocumento.CedulaNacional or TiposDocumento.Dimex;

        // Qué se espera del número. Lo muestra la pantalla como ayuda y lo usa el
        // validador en el mensaje de error: un solo texto para las dos cosas.
        public static string DescribirNumIdentidad(string? tipoDocumento) => tipoDocumento switch
        {
            TiposDocumento.CedulaNacional =>
                $"{DigitosCedulaNacional} dígitos numéricos, sin guiones ni otros símbolos.",
            TiposDocumento.Dimex =>
                $"{DigitosMinimosDimex} o {DigitosMaximosDimex} dígitos numéricos, sin guiones ni otros símbolos.",
            TiposDocumento.Pasaporte =>
                $"Números, letras y guiones, hasta {LongitudMaximaNumIdentidad} caracteres, según el país emisor.",
            TiposDocumento.Otro =>
                $"Números, letras y guiones, hasta {LongitudMaximaNumIdentidad} caracteres.",
            _ => string.Empty
        };

        // Predicado único de formato. Lo usan el validador (para aceptar o rechazar)
        // y la pantalla (para decidir si el número escrito sirve para el tipo nuevo).
        // Espera un valor ya normalizado con TextoNormalizador.NormalizarNumeroDocumento.
        public static bool TieneFormatoNumIdentidad(string? tipoDocumento, string? numero) => tipoDocumento switch
        {
            TiposDocumento.CedulaNacional => TieneFormatoCedulaNacional(numero),
            TiposDocumento.Dimex => TieneFormatoDimex(numero),
            // El pasaporte es de longitud variable según el país emisor, pero con
            // los mismos caracteres que "Otro": es lo que filtra la pantalla y lo
            // que promete la ayuda del campo.
            TiposDocumento.Pasaporte => EsAlfanumericoConGuiones(numero),
            TiposDocumento.Otro => EsAlfanumericoConGuiones(numero),
            _ => false
        };

        // Descarta los caracteres que no corresponden al tipo y recorta al máximo.
        // Es para filtrar lo que se escribe en la pantalla; no reemplaza a la
        // validación, que vuelve a comprobar el valor completo del lado del servidor.
        public static string FiltrarNumIdentidad(string? tipoDocumento, string? valor)
        {
            if (string.IsNullOrEmpty(valor))
                return string.Empty;

            var soloDigitos = NumIdentidadEsSoloDigitos(tipoDocumento);
            var maximo = LongitudMaximaNumIdentidadPara(tipoDocumento);
            var filtrado = new StringBuilder(valor.Length);

            foreach (var caracter in valor)
            {
                // El guion se admite en pasaporte y "Otro": los formatos varían por
                // país y filtrarlo impediría cargar un documento legítimo.
                var permitido = soloDigitos
                    ? char.IsAsciiDigit(caracter)
                    : char.IsAsciiLetterOrDigit(caracter) || caracter == '-';

                if (permitido)
                    filtrado.Append(caracter);

                if (filtrado.Length == maximo)
                    break;
            }

            return filtrado.ToString();
        }

        public static string ComponerNombreCompleto(
            string? primerNombre, string? segundoNombre, string? primerApellido, string? segundoApellido) =>
            string.Join(' ', new[] { primerNombre, segundoNombre, primerApellido, segundoApellido }
                .Where(parte => !string.IsNullOrWhiteSpace(parte))
                .Select(parte => parte!.Trim()));
    }
}
