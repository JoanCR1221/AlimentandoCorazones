namespace SIGAC.Domain
{
    // Largos máximos y valores por defecto del gasto operativo y del tipo de gasto.
    // Fuente única para la configuración de columnas en SigacDbContext, el
    // validador y los MaxLength de los formularios: si cambia una columna, se
    // cambia acá y los tres quedan alineados. Mismo criterio que ReglasBeneficiario.
    public static class ReglasGastoOperativo
    {
        public const int LongitudMaximaProveedor = 150;
        public const int LongitudMaximaNumeroFactura = 50;
        public const int LongitudMaximaNumeroCheque = 50;
        public const int LongitudMaximaFormaPago = 30;
        public const int LongitudMaximaCuentaContable = 100;
        public const int LongitudMaximaDescripcionCuenta = 100;
        public const int LongitudMaximaDescripcion = 500;
        public const int LongitudMaximaResponsable = 150;
        public const int LongitudMaximaMotivoAnulacion = 500;

        public const int LongitudMaximaNombreTipo = 100;

        // Valores que traen las cuatro planillas mensuales revisadas en todas sus
        // líneas. Son los defaults de las columnas y los que precarga el
        // formulario, que los deja editables.
        public const string CuentaContablePorDefecto = "1 CAJA Y BANCOS";
        public const string DescripcionCuentaPorDefecto = "GASTOS ADMINISTRATIVOS";
    }
}
