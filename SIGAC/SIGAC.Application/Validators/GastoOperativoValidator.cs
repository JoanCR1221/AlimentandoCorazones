using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    // Datos de un gasto ya validados y normalizados, listos para persistir.
    // El servicio solo los copia a la entidad: no vuelve a limpiar ni a decidir nada.
    public sealed record GastoOperativoValidado(
        int TipoGastoId,
        string Proveedor,
        string NumeroFactura,
        DateTime Fecha,
        decimal MontoSinIva,
        decimal Iva,
        string Moneda,
        string FormaPago,
        string? NumeroCheque,
        string CuentaContable,
        string DescripcionCuenta,
        string Descripcion,
        string Responsable);

    // Toda la validación y normalización de entrada del gasto, centralizada por
    // el mismo motivo que ArticuloValidator y BeneficiarioValidator.
    //
    // Los largos máximos salen de ReglasGastoOperativo, la misma fuente que usa
    // SigacDbContext para las columnas: no pueden desalinearse.
    //
    // Que el tipo de gasto exista y esté activo NO se valida acá: hace falta la
    // base, y este validador es puro. Lo resuelve GastosService.
    public static class GastoOperativoValidator
    {
        // Recibe el DTO y no los campos sueltos: con trece valores, varios del
        // mismo tipo (string), una lista de parámetros posicionales permitía
        // cruzar dos argumentos sin que el compilador lo notara.
        public static GastoOperativoValidado Validar(IDatosGastoOperativo dto) =>
            new(
                ValidarTipoGastoId(dto.TipoGastoId),
                ValidarTextoObligatorio(dto.Proveedor, "El proveedor", "El proveedor es obligatorio.", ReglasGastoOperativo.LongitudMaximaProveedor),
                ValidarTextoObligatorio(dto.NumeroFactura, "El número de factura", "El número de factura es obligatorio.", ReglasGastoOperativo.LongitudMaximaNumeroFactura),
                ValidarFecha(dto.Fecha),
                ValidarMontoSinIva(dto.MontoSinIva),
                ValidarIva(dto.Iva),
                ValidarMoneda(dto.Moneda),
                ValidarFormaPago(dto.FormaPago),
                ValidarNumeroCheque(dto.NumeroCheque),
                ValidarTextoObligatorio(dto.CuentaContable, "La cuenta contable", "La cuenta contable es obligatoria.", ReglasGastoOperativo.LongitudMaximaCuentaContable),
                ValidarTextoObligatorio(dto.DescripcionCuenta, "La descripción de cuenta", "La descripción de cuenta es obligatoria.", ReglasGastoOperativo.LongitudMaximaDescripcionCuenta),
                ValidarTextoObligatorio(dto.Descripcion, "La descripción", "La descripción es obligatoria.", ReglasGastoOperativo.LongitudMaximaDescripcion),
                ValidarTextoObligatorio(dto.Responsable, "El responsable", "El responsable es obligatorio.", ReglasGastoOperativo.LongitudMaximaResponsable));

        public static int ValidarTipoGastoId(int tipoGastoId)
        {
            if (tipoGastoId <= 0)
                throw new ValidationException("El tipo de gasto es obligatorio.");

            return tipoGastoId;
        }

        // Se elige de una lista en la pantalla, no se teclea, pero se comprueba
        // igual contra el catálogo cerrado: mismo criterio que
        // DonanteValidator.ValidarTipoPersona.
        public static string ValidarMoneda(string? moneda)
        {
            var normalizada = TextoNormalizador.CompactarEspacios(moneda);

            if (normalizada.Length == 0)
                throw new ValidationException("La moneda es obligatoria.");

            if (!TiposMoneda.EsValido(normalizada))
            {
                throw new ValidationException(
                    $"La moneda '{normalizada}' no es válida. " +
                    $"Valores válidos: {string.Join(", ", TiposMoneda.Todos)}.");
            }

            return normalizada;
        }

        public static string ValidarFormaPago(string? formaPago)
        {
            var normalizada = TextoNormalizador.CompactarEspacios(formaPago);

            if (normalizada.Length == 0)
                throw new ValidationException("La forma de pago es obligatoria.");

            if (!FormasPago.EsValido(normalizada))
            {
                throw new ValidationException(
                    $"La forma de pago '{normalizada}' no es válida. " +
                    $"Valores válidos: {string.Join(", ", FormasPago.Todos)}.");
            }

            return normalizada;
        }

        // Neto sin IVA: la columna MONTO del reporte de la contadora.
        public static decimal ValidarMontoSinIva(decimal montoSinIva)
        {
            if (montoSinIva <= 0)
                throw new ValidationException("El monto sin IVA debe ser mayor a 0.");

            return montoSinIva;
        }

        // 0 es válido: varias líneas del reporte vienen sin IVA.
        public static decimal ValidarIva(decimal iva)
        {
            if (iva < 0)
                throw new ValidationException("El IVA no puede ser negativo.");

            return iva;
        }

        // TODO: confirmar con la contadora cuándo es obligatorio el número de
        // cheque. Se pensó en exigirlo para pagos a crédito, pero en el reporte
        // el cheque aparece en pagos de contado hechos por banco, así que por
        // ahora es opcional siempre. Vacío se guarda como NULL.
        public static string? ValidarNumeroCheque(string? numeroCheque)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(numeroCheque);

            if (normalizado.Length == 0)
                return null;

            if (normalizado.Length > ReglasGastoOperativo.LongitudMaximaNumeroCheque)
                throw new ValidationException(
                    $"El número de cheque no puede superar los {ReglasGastoOperativo.LongitudMaximaNumeroCheque} caracteres.");

            return normalizado;
        }

        // Igual que la fecha de una entrada de inventario: el gasto registra algo
        // que ya ocurrió, así que una fecha futura no representa un movimiento real.
        public static DateTime ValidarFecha(DateTime fecha)
        {
            if (fecha == default)
                throw new ValidationException("La fecha es obligatoria.");

            if (fecha.Date > DateTime.Today)
                throw new ValidationException("La fecha del gasto no puede ser futura.");

            return fecha.Date;
        }

        public static string ValidarMotivoAnulacion(string? motivo) =>
            ValidarTextoObligatorio(motivo, "El motivo de anulación", "El motivo de anulación es obligatorio.", ReglasGastoOperativo.LongitudMaximaMotivoAnulacion);

        // Compacta espacios (también en el proveedor: así "Coopeagua  R.L." y
        // "Coopeagua R.L." quedan iguales y el autocompletado no los ofrece dos
        // veces), exige que no quede vacío y que entre en la columna. El mensaje
        // de obligatorio llega armado porque el género cambia según el campo.
        private static string ValidarTextoObligatorio(string? valor, string campo, string mensajeObligatorio, int longitudMaxima)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            if (normalizado.Length == 0)
                throw new ValidationException(mensajeObligatorio);

            if (normalizado.Length > longitudMaxima)
                throw new ValidationException($"{campo} no puede superar los {longitudMaxima} caracteres.");

            return normalizado;
        }
    }
}
