namespace SIGAC.Domain.Entities
{
    // Tipo de gasto operativo ("Combustible", "Servicio de Agua"...). Es la misma
    // agrupación que usa el reporte mensual de la contadora, y vive en tabla y no
    // en una clase estática porque la lista la mantiene la asociación desde
    // /gastos/tipos: en cuatro meses de reportes aparecieron ocho tipos distintos,
    // y con un catálogo en código cada tipo nuevo exigía una migración.
    //
    // Nunca se borra: los gastos registrados lo referencian con una FK Restrict.
    // Un tipo que ya no se usa se desactiva (Activo = false), sale de los
    // desplegables y sigue mostrándose en los gastos que ya lo tienen.
    public class TipoGasto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;

        // El gasto de este tipo es una compra que ingresa al inventario: es lo que
        // decide si RegistrarGasto ofrece cargar la entrada y si el gasto aparece
        // en el selector de RegistrarEntradaInventario. Antes eso se decidía
        // comparando contra la constante "CompraInsumos", que acoplaba los dos
        // módulos por un texto.
        public bool GeneraInventario { get; set; }

        // Cuenta con la que se precarga el formulario al elegir este tipo. Es solo
        // una sugerencia: el gasto guarda su propia CuentaContable.
        public string? CuentaContablePorDefecto { get; set; }
    }
}
