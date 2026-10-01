namespace SIGAC.Domain.Entities
{
    public enum EstadoGastoOperativo
    {
        Activo,
        Anulado
    }

    // Entidad del módulo de Gastos Operativos. Lleva las columnas del reporte
    // mensual de la contadora (proveedor, factura, día, monto, IVA, cheque y
    // cuenta) para que SIGAC pueda reproducirlo sin depender de su software.
    //
    // Estado y FormaPago son dominios cerrados (CK_GastosOperativos_Estado y
    // CK_GastosOperativos_FormaPago); el tipo de gasto es una tabla (TiposGasto).
    public class GastoOperativo
    {
        public int Id { get; set; }

        public int TipoGastoId { get; set; }
        public TipoGasto? TipoGasto { get; set; }

        public string Proveedor { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = string.Empty;

        // Neto, SIN impuesto: es la columna MONTO del reporte. El nombre lo dice
        // para que nadie vuelva a preguntarse si incluye IVA. Lo que salió de la
        // cuenta es MontoSinIva + Iva.
        public decimal MontoSinIva { get; set; }

        // Puede ser 0: varias líneas del reporte vienen sin IVA.
        public decimal Iva { get; set; }

        // Valor del catálogo cerrado TiposMoneda ("Colones", "Dólares", "Euros").
        // Sin ella, un monto de 100 no dice si son 100 colones o 100 dólares.
        public string Moneda { get; set; } = string.Empty;

        // Valor del catálogo cerrado FormasPago.
        public string FormaPago { get; set; } = string.Empty;
        public string? NumeroCheque { get; set; }

        public string CuentaContable { get; set; } = string.Empty;
        public string DescripcionCuenta { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
        public EstadoGastoOperativo Estado { get; set; } = EstadoGastoOperativo.Activo;
        public DateTime FechaRegistro { get; set; }
        public string? MotivoAnulacion { get; set; }
    }
}
