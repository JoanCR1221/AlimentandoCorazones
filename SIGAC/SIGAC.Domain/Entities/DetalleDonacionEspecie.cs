namespace SIGAC.Domain.Entities
{
    // Una línea de una donación en especie: un artículo donado con su cantidad.
    public class DetalleDonacionEspecie
    {
        public int Id { get; set; }

        public int DonacionEspecieId { get; set; }
        public DonacionEspecie? DonacionEspecie { get; set; }

        // Texto libre y no un ArticuloId: lo donado puede no existir todavía en el
        // catálogo de inventario. El servicio resolverá después si corresponde a un
        // artículo existente o si hay que darlo de alta; obligar a elegir de una
        // lista impediría registrar la donación en el momento de recibirla.
        public string NombreArticulo { get; set; } = string.Empty;

        public int Cantidad { get; set; }

        // Valor del catálogo cerrado CategoriasArticulo, el mismo que usa Articulo:
        // así lo donado se puede dar de alta en inventario sin traducir categorías.
        public string Categoria { get; set; } = string.Empty;

        // Debe ser una unidad válida PARA esa categoría, no cualquiera de la lista
        // completa: se valida con UnidadesMedidaArticulo.EsValidaParaCategoria, para
        // no acabar con "Calzado" medido en "Litro".
        public string UnidadMedida { get; set; } = string.Empty;
    }
}
