using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    // Datos de un donante ya validados y normalizados, listos para persistir.
    // El servicio solo los copia a la entidad: no vuelve a limpiar ni a decidir nada.
    public sealed record DonanteValidado(
        string Nombre,
        string TipoPersona,
        string? Telefono,
        string? Correo);

    // Toda la normalización y validación de entrada del donante, centralizada por
    // el mismo motivo que ArticuloValidator: el nombre se compara contra los
    // donantes ya registrados (ExisteNombreAsync, para avisar de homónimos) y esa
    // comparación es exacta, así que la forma en que se COMPARA tiene que ser
    // exactamente la misma en que se GUARDA.
    //
    // Sin compactar los espacios antes de comparar, " Ana  Rojas" y "Ana Rojas"
    // serían dos donantes distintos y el aviso de duplicado no saltaría nunca. La
    // diferencia con Articulo es que acá no hay índice único que lo respalde: si el
    // aviso no salta, el duplicado entra y nadie lo detecta.
    public static class DonanteValidator
    {
        // Espejo de las longitudes declaradas en SigacDbContext para las columnas
        // de Donantes (las mismas que LongitudesDonaciones usa en los DTOs). Se
        // validan acá para dar un mensaje entendible en vez de dejar que la base
        // rechace el INSERT con un error sin traducir.
        //
        // Están duplicadas respecto de LongitudesDonaciones a propósito:
        // aquella es internal y vive con los DTOs (valida el formulario), esta es
        // la barrera del servidor y acompaña al resto de los validadores, que ya
        // declaran sus propias constantes (ver ArticuloValidator).
        public const int LongitudMaximaNombre = 150;
        public const int LongitudMaximaTipoPersona = 20;
        public const int LongitudMaximaTelefono = 20;
        public const int LongitudMaximaCorreo = 150;

        public static DonanteValidado Validar(DonanteCrearDto dto) =>
            Validar(dto.Nombre, dto.TipoPersona, dto.Telefono, dto.Correo);

        public static DonanteValidado Validar(DonanteEditarDto dto) =>
            Validar(dto.Nombre, dto.TipoPersona, dto.Telefono, dto.Correo);

        // Sobrecarga para el donante que se registra sin salir del formulario de
        // donación: son los mismos campos y la misma regla, así que se valida con
        // el mismo código y no con una copia paralela que pueda divergir.
        public static DonanteValidado Validar(NuevoDonanteDto dto) =>
            Validar(dto.Nombre, dto.TipoPersona, dto.Telefono, dto.Correo);

        public static DonanteValidado Validar(
            string? nombre, string? tipoPersona, string? telefono, string? correo) =>
            new(
                ValidarNombre(nombre),
                ValidarTipoPersona(tipoPersona),
                ValidarTelefono(telefono),
                ValidarCorreo(correo));

        // Devuelve el nombre tal como se guarda: sin espacios en los extremos ni
        // internos repetidos. Es también el valor con el que hay que buscar posibles
        // homónimos, para que la búsqueda y el guardado hablen del mismo texto.
        //
        // Se usa CompactarEspacios y NO TextoNormalizador.NormalizarNombre, igual
        // que en ArticuloValidator: NormalizarNombre pone en mayúscula la primera
        // letra, algo pensado para nombres de persona, y un donante puede ser una
        // razón social que empiece de otra forma ("del Valle S.A."). Compactar
        // alcanza para que la comparación sea estable.
        public static string ValidarNombre(string? valor)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            if (normalizado.Length == 0)
                throw new ValidationException("El nombre del donante es obligatorio.");

            if (normalizado.Length > LongitudMaximaNombre)
                throw new ValidationException(
                    $"El nombre no puede superar los {LongitudMaximaNombre} caracteres.");

            return normalizado;
        }

        // Se elige de una lista en la pantalla, no se teclea, pero se comprueba
        // igual contra el catálogo cerrado: sin esto un valor inventado desde otro
        // camino solo se detectaría cuando el CHECK CK_Donantes_TipoPersona lo
        // rechazara con un error genérico.
        public static string ValidarTipoPersona(string? valor)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            if (normalizado.Length == 0)
                throw new ValidationException("El tipo de persona es obligatorio.");

            if (normalizado.Length > LongitudMaximaTipoPersona)
                throw new ValidationException(
                    $"El tipo de persona no puede superar los {LongitudMaximaTipoPersona} caracteres.");

            if (!TiposPersonaDonante.EsValido(normalizado))
            {
                throw new ValidationException(
                    $"El tipo de persona '{normalizado}' no es válido. " +
                    $"Valores válidos: {string.Join(", ", TiposPersonaDonante.Todos)}.");
            }

            return normalizado;
        }

        // Opcional: se recibe la donación igual aunque el donante no deje teléfono.
        // Cuando no se ingresa se guarda como NULL y no como cadena vacía, para que
        // la columna distinga "no dejó teléfono" de "dejó uno vacío".
        //
        // Sin validación de formato, a diferencia de BeneficiarioValidator (que
        // exige los 8 dígitos de Costa Rica vía ReglasBeneficiario): la columna se
        // dimensionó en 20 justamente porque un donante puede ser una empresa con
        // extensión o estar en el extranjero, así que no hay un formato único que
        // exigir. Solo se compacta y se controla la longitud.
        public static string? ValidarTelefono(string? valor)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            if (normalizado.Length == 0)
                return null;

            if (normalizado.Length > LongitudMaximaTelefono)
                throw new ValidationException(
                    $"El teléfono no puede superar los {LongitudMaximaTelefono} caracteres.");

            return normalizado;
        }

        // Opcional, mismo tratamiento que el teléfono: vacío se guarda como NULL.
        //
        // Sin validación de formato acá: el [EmailAddress] del DTO ya la hace en el
        // formulario, y replicar una regla de formato de correo en el servidor
        // significa mantener dos criterios que se van a contradecir. Lo que sí se
        // controla es la longitud, que es lo que la base puede rechazar.
        public static string? ValidarCorreo(string? valor)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            if (normalizado.Length == 0)
                return null;

            if (normalizado.Length > LongitudMaximaCorreo)
                throw new ValidationException(
                    $"El correo no puede superar los {LongitudMaximaCorreo} caracteres.");

            return normalizado;
        }
    }
}
