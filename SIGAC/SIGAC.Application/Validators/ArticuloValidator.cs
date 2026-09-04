using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    // Datos de un artículo ya validados y normalizados, listos para persistir.
    // El servicio solo los copia a la entidad: no vuelve a limpiar ni a decidir nada.
    public sealed record ArticuloValidado(
        string Nombre,
        string? Codigo,
        string Categoria,
        string UnidadMedida,
        string? Ubicacion,
        int StockMinimo);

    // Toda la normalización y validación de entrada del artículo, centralizada por
    // el mismo motivo que BeneficiarioValidator: el nombre es la clave natural del
    // catálogo (índice único UX_Articulos_Nombre), así que la forma en que se
    // COMPARA tiene que ser exactamente la misma en que se GUARDA.
    //
    // Sin compactar los espacios antes de comparar, " Arroz" no encontraba a
    // "Arroz" y se creaba un segundo artículo: el stock quedaba partido entre dos
    // filas y el índice único no lo impedía, porque para SQL Server esas dos
    // cadenas son distintas. (El espacio al FINAL sí lo neutraliza SQL Server solo,
    // por el relleno ANSI de varchar; el del inicio y los internos repetidos no.)
    public static class ArticuloValidator
    {
        // Espejo de las longitudes declaradas en SigacDbContext para las columnas
        // de Articulos. Se validan acá para dar un mensaje entendible en vez de
        // dejar que la base rechace el INSERT con un error sin traducir.
        public const int LongitudMaximaNombre = 150;
        public const int LongitudMaximaCodigo = 50;
        public const int LongitudMaximaCategoria = 100;
        public const int LongitudMaximaUnidadMedida = 50;
        public const int LongitudMaximaUbicacion = 100;

        // Valida y normaliza el formulario de edición completo.
        public static ArticuloValidado Validar(ArticuloEditarDto dto)
        {
            if (dto.StockMinimo < 0)
                throw new ValidationException("El stock mínimo no puede ser negativo.");

            var (categoria, unidadMedida) = ValidarCategoriaYUnidad(dto.Categoria, dto.UnidadMedida);

            return new ArticuloValidado(
                ValidarNombre(dto.Nombre),
                ValidarCodigo(dto.Codigo),
                categoria,
                unidadMedida,
                ValidarUbicacion(dto.Ubicacion),
                dto.StockMinimo);
        }

        // Categoría y unidad de medida se validan JUNTAS y no por separado porque la
        // unidad no es válida en abstracto: "Litro" existe, pero no para "Calzado".
        // Devuelve los dos valores ya compactados, que son los que hay que persistir.
        //
        // Vive acá y no en el servicio para que el alta (que trae los valores en
        // EntradaInventarioCrearDto) y la edición (que los trae en ArticuloEditarDto)
        // apliquen exactamente la misma regla, sin duplicarla en dos métodos.
        public static (string Categoria, string UnidadMedida) ValidarCategoriaYUnidad(
            string? categoria, string? unidadMedida)
        {
            var categoriaNormalizada = ValidarObligatorio(categoria, "La categoría", LongitudMaximaCategoria);
            var unidadNormalizada = ValidarObligatorio(unidadMedida, "La unidad de medida", LongitudMaximaUnidadMedida);

            if (!CategoriasArticulo.EsValido(categoriaNormalizada))
            {
                throw new ValidationException(
                    $"La categoría '{categoriaNormalizada}' no es válida. " +
                    $"Categorías válidas: {string.Join(", ", CategoriasArticulo.Todos)}.");
            }

            if (!UnidadesMedidaArticulo.EsValidaParaCategoria(categoriaNormalizada, unidadNormalizada))
            {
                var validas = UnidadesMedidaArticulo.ObtenerUnidadesValidas(categoriaNormalizada);

                throw new ValidationException(
                    $"La unidad de medida '{unidadNormalizada}' no es válida para la categoría " +
                    $"'{categoriaNormalizada}'. Unidades válidas: {string.Join(", ", validas)}.");
            }

            return (categoriaNormalizada, unidadNormalizada);
        }

        // Devuelve el nombre tal como se guarda: sin espacios en los extremos ni
        // internos repetidos. Es también el valor con el que hay que buscar el
        // artículo, para que la búsqueda y el guardado hablen del mismo texto.
        //
        // Se usa CompactarEspacios y NO TextoNormalizador.NormalizarNombre: ese
        // segundo también pone la primera letra en mayúscula, que tiene sentido
        // para nombres de personas pero cambiaría "arroz" por "Arroz" sin que nadie
        // lo pidiera. Para la unicidad no hace falta: la collation CI de SQL Server
        // ya trata "arroz" y "Arroz" como el mismo artículo.
        public static string ValidarNombre(string? valor)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            if (normalizado.Length == 0)
                throw new ValidationException("El nombre es obligatorio.");

            if (normalizado.Length > LongitudMaximaNombre)
                throw new ValidationException(
                    $"El nombre no puede superar los {LongitudMaximaNombre} caracteres.");

            return normalizado;
        }

        // Opcional: no todo artículo tiene código. Cuando no se ingresa se guarda
        // como NULL y no como cadena vacía, que es lo que espera el filtro del
        // índice único UX_Articulos_Codigo ([Codigo] IS NOT NULL AND [Codigo] <> '').
        public static string? ValidarCodigo(string? valor)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            if (normalizado.Length == 0)
                return null;

            if (normalizado.Length > LongitudMaximaCodigo)
                throw new ValidationException(
                    $"El código no puede superar los {LongitudMaximaCodigo} caracteres.");

            return normalizado;
        }

        // Opcional: texto libre ("Bodega 2, estante A"), así que solo se compacta y
        // se controla la longitud.
        public static string? ValidarUbicacion(string? valor)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            if (normalizado.Length == 0)
                return null;

            if (normalizado.Length > LongitudMaximaUbicacion)
                throw new ValidationException(
                    $"La ubicación no puede superar los {LongitudMaximaUbicacion} caracteres.");

            return normalizado;
        }

        // Categoría y unidad de medida se eligen de una lista en la pantalla, no se
        // teclean, así que acá solo se compactan y se comprueba que hayan llegado.
        private static string ValidarObligatorio(string? valor, string etiqueta, int longitudMaxima)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            if (normalizado.Length == 0)
                throw new ValidationException($"{etiqueta} es obligatoria.");

            if (normalizado.Length > longitudMaxima)
                throw new ValidationException(
                    $"{etiqueta} no puede superar los {longitudMaxima} caracteres.");

            return normalizado;
        }
    }
}
