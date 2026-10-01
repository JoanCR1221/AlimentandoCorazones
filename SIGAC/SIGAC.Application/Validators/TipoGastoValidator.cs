using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    public sealed record TipoGastoValidado(string Nombre, bool GeneraInventario, string? CuentaContablePorDefecto);

    // Validación y normalización del alta/edición de un tipo de gasto. La
    // unicidad del nombre no se valida acá (hace falta la base): la resuelve
    // TiposGastoService, y UX_TiposGasto_Nombre es la red final.
    public static class TipoGastoValidator
    {
        public static TipoGastoValidado Validar(TipoGastoGuardarDto dto)
        {
            var nombre = TextoNormalizador.CompactarEspacios(dto.Nombre);

            if (nombre.Length == 0)
                throw new ValidationException("El nombre del tipo de gasto es obligatorio.");

            if (nombre.Length > ReglasGastoOperativo.LongitudMaximaNombreTipo)
                throw new ValidationException(
                    $"El nombre no puede superar los {ReglasGastoOperativo.LongitudMaximaNombreTipo} caracteres.");

            // Opcional: vacío se guarda como NULL y el formulario de gastos cae al
            // default general (ReglasGastoOperativo.CuentaContablePorDefecto).
            var cuenta = TextoNormalizador.CompactarEspacios(dto.CuentaContablePorDefecto);

            if (cuenta.Length > ReglasGastoOperativo.LongitudMaximaCuentaContable)
                throw new ValidationException(
                    $"La cuenta contable no puede superar los {ReglasGastoOperativo.LongitudMaximaCuentaContable} caracteres.");

            return new TipoGastoValidado(nombre, dto.GeneraInventario, cuenta.Length == 0 ? null : cuenta);
        }
    }
}
