using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Application.Validators;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    // Orquesta el alta, la edición y la anulación de gastos operativos: valida
    // con GastoOperativoValidator, arma la entidad y delega en el repositorio.
    // Las reglas de validación no viven acá, salvo la del tipo de gasto, que
    // necesita la base.
    public class GastosService : IGastosService
    {
        // Sugerencias que ofrece el autocompletado de proveedor: suficientes para
        // elegir sin tener que seguir escribiendo, pocas para que la lista no tape
        // el formulario.
        private const int MaximoProveedoresSugeridos = 10;

        private readonly IGastosRepository _repository;
        private readonly ITiposGastoRepository _tipos;
        private readonly IBitacoraService _bitacora;

        public GastosService(IGastosRepository repository, ITiposGastoRepository tipos, IBitacoraService bitacora)
        {
            _repository = repository;
            _tipos = tipos;
            _bitacora = bitacora;
        }

        public async Task<int> RegistrarGastoAsync(GastoOperativoCrearDto dto)
        {
            try
            {
                var datos = GastoOperativoValidator.Validar(dto);
                var tipo = await ValidarTipoGastoAsync(datos.TipoGastoId, tipoActualDelGasto: null);

                var gasto = new GastoOperativo
                {
                    Estado = EstadoGastoOperativo.Activo,
                    FechaRegistro = DateTime.Now
                };
                CopiarDatos(datos, gasto);

                await _repository.AgregarAsync(gasto);

                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Gastos,
                    DescribirParaBitacora(gasto, tipo));

                return gasto.Id;
            }
            catch (Exception ex) when (ex is not ValidationException)
            {
                throw new Exception("Error al registrar el gasto operativo.", ex);
            }
        }

        public async Task<GastoOperativoEditarDto?> ObtenerParaEditarAsync(int id)
        {
            try
            {
                var gasto = await _repository.ObtenerPorIdAsync(id);
                if (gasto is null)
                    return null;

                return new GastoOperativoEditarDto
                {
                    TipoGastoId = gasto.TipoGastoId,
                    Proveedor = gasto.Proveedor,
                    NumeroFactura = gasto.NumeroFactura,
                    Fecha = gasto.Fecha,
                    MontoSinIva = gasto.MontoSinIva,
                    Iva = gasto.Iva,
                    Moneda = gasto.Moneda,
                    FormaPago = gasto.FormaPago,
                    NumeroCheque = gasto.NumeroCheque,
                    CuentaContable = gasto.CuentaContable,
                    DescripcionCuenta = gasto.DescripcionCuenta,
                    Descripcion = gasto.Descripcion,
                    Responsable = gasto.Responsable
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el gasto operativo.", ex);
            }
        }

        public async Task EditarGastoAsync(int id, GastoOperativoEditarDto dto)
        {
            try
            {
                var gasto = await _repository.ObtenerPorIdAsync(id)
                    ?? throw new NotFoundException("El gasto operativo no existe.");

                // Un gasto anulado es un registro cerrado: editarlo cambiaría el
                // respaldo contable de una anulación ya comunicada. Para corregirlo
                // hay que registrar uno nuevo, no reabrir el anulado.
                if (gasto.Estado == EstadoGastoOperativo.Anulado)
                    throw new ValidationException("No se puede editar un gasto operativo anulado.");

                var datos = GastoOperativoValidator.Validar(dto);
                var tipo = await ValidarTipoGastoAsync(datos.TipoGastoId, tipoActualDelGasto: gasto.TipoGastoId);

                CopiarDatos(datos, gasto);

                await _repository.ActualizarAsync(gasto);

                await _bitacora.RegistrarAsync(AccionesBitacora.Editar, ModulosSistema.Gastos,
                    DescribirParaBitacora(gasto, tipo));
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al editar el gasto operativo.", ex);
            }
        }

        public async Task<GastosConsultaDto> ObtenerGastosAsync(FiltrosGastoDto filtros)
        {
            try
            {
                var gastos = await _repository.ObtenerTodosAsync(filtros);

                var lista = gastos
                    .Select(g => new GastoOperativoListaDto
                    {
                        Id = g.Id,
                        TipoGastoId = g.TipoGastoId,
                        TipoGasto = g.TipoGasto?.Nombre ?? string.Empty,
                        Proveedor = g.Proveedor,
                        NumeroFactura = g.NumeroFactura,
                        Fecha = g.Fecha,
                        MontoSinIva = g.MontoSinIva,
                        Iva = g.Iva,
                        Moneda = g.Moneda,
                        FormaPago = g.FormaPago,
                        NumeroCheque = g.NumeroCheque,
                        CuentaContable = g.CuentaContable,
                        DescripcionCuenta = g.DescripcionCuenta,
                        Descripcion = g.Descripcion,
                        Responsable = g.Responsable,
                        Estado = g.Estado.ToString()
                    })
                    .ToList();

                // Un total por cada moneda presente, no un solo decimal: sumar
                // colones con dólares en un único número no representaría nada.
                // Excluye los anulados: un gasto anulado ya no representa dinero
                // efectivamente gastado, así que sumarlo distorsionaría el total del
                // período consultado.
                //
                // Suma Total (MontoSinIva + Iva) y no solo el neto: lo que la gente
                // espera ver en "total gastado" es lo que salió de la cuenta, y eso
                // incluye el impuesto. El neto por separado es asunto del reporte de
                // la contadora, que totaliza MONTO e I.V.A. en columnas distintas.
                var totalesPorMoneda = lista
                    .Where(g => g.Estado == nameof(EstadoGastoOperativo.Activo))
                    .GroupBy(g => g.Moneda)
                    .Select(g => new MontoPorMonedaDto(g.Key, g.Sum(x => x.Total)))
                    .ToList();

                return new GastosConsultaDto(lista, totalesPorMoneda);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar los gastos operativos.", ex);
            }
        }

        public async Task AnularGastoAsync(AnulacionGastoDto dto)
        {
            try
            {
                var gasto = await _repository.ObtenerPorIdAsync(dto.GastoId)
                    ?? throw new NotFoundException("El gasto operativo no existe.");

                if (gasto.Estado == EstadoGastoOperativo.Anulado)
                    throw new ValidationException("El gasto operativo ya está anulado.");

                var motivo = GastoOperativoValidator.ValidarMotivoAnulacion(dto.MotivoAnulacion);

                // Una sola llamada, y no anular acá y pedirle después al inventario
                // que revierta lo suyo: los dos pasos son todo-o-nada y cada uno con
                // su propia transacción no podía serlo. El repositorio resuelve
                // adentro cuántas entradas hay que revertir, incluido el caso de
                // ninguna: el enlace de AB#2501 es manual, así que un gasto de compra
                // sin entrada cargada es válido y no es un error.
                //
                // No depende del tipo de gasto: se revierten las entradas que estén
                // vinculadas, sea cual sea el tipo (ni siquiera se mira
                // GeneraInventario, que solo decide qué ofrecen las pantallas).
                await _repository.AnularConEntradasVinculadasAsync(dto.GastoId, motivo);

                await _bitacora.RegistrarAsync(AccionesBitacora.Anular, ModulosSistema.Gastos,
                    $"Gasto #{gasto.Id}: {gasto.Proveedor}, fact. {gasto.NumeroFactura}. Motivo: {motivo}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al anular el gasto operativo.", ex);
            }
        }

        public async Task<IReadOnlyList<TipoGastoDto>> ObtenerTiposActivosAsync(int? incluirTipoId = null)
        {
            try
            {
                var tipos = await _tipos.ObtenerTodosAsync(soloActivos: false);

                return tipos
                    .Where(t => t.Activo || t.Id == incluirTipoId)
                    .Select(TiposGastoService.ADto)
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar los tipos de gasto.", ex);
            }
        }

        public async Task<IReadOnlyList<string>> BuscarProveedoresAsync(string texto)
        {
            try
            {
                var busqueda = TextoNormalizador.CompactarEspacios(texto);
                if (busqueda.Length == 0)
                    return Array.Empty<string>();

                return await _repository.BuscarProveedoresAsync(busqueda, MaximoProveedoresSugeridos);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al buscar proveedores.", ex);
            }
        }

        // El tipo tiene que existir y estar activo. Excepción: al editar, el tipo
        // que el gasto YA tenía se acepta aunque se haya desactivado después; si
        // no, cualquier corrección de un gasto viejo (un número de factura mal
        // tecleado) obligaría a cambiarle también el tipo.
        private async Task<TipoGasto> ValidarTipoGastoAsync(int tipoGastoId, int? tipoActualDelGasto)
        {
            var tipo = await _tipos.ObtenerPorIdAsync(tipoGastoId)
                ?? throw new ValidationException("El tipo de gasto elegido no existe.");

            if (!tipo.Activo && tipo.Id != tipoActualDelGasto)
                throw new ValidationException($"El tipo de gasto '{tipo.Nombre}' está inactivo y no se puede usar en gastos nuevos.");

            return tipo;
        }

        // Un solo lugar que pasa los datos validados a la entidad, para alta y
        // edición: un campo nuevo se agrega acá una vez y no en dos mapeos.
        private static void CopiarDatos(GastoOperativoValidado datos, GastoOperativo gasto)
        {
            gasto.TipoGastoId = datos.TipoGastoId;
            gasto.Proveedor = datos.Proveedor;
            gasto.NumeroFactura = datos.NumeroFactura;
            gasto.Fecha = datos.Fecha;
            gasto.MontoSinIva = datos.MontoSinIva;
            gasto.Iva = datos.Iva;
            gasto.Moneda = datos.Moneda;
            gasto.FormaPago = datos.FormaPago;
            gasto.NumeroCheque = datos.NumeroCheque;
            gasto.CuentaContable = datos.CuentaContable;
            gasto.DescripcionCuenta = datos.DescripcionCuenta;
            gasto.Descripcion = datos.Descripcion;
            gasto.Responsable = datos.Responsable;
        }

        private static string DescribirParaBitacora(GastoOperativo gasto, TipoGasto tipo) =>
            $"Gasto #{gasto.Id}: {tipo.Nombre}, {gasto.Proveedor}, fact. {gasto.NumeroFactura}, " +
            $"{gasto.MontoSinIva:N2} + IVA {gasto.Iva:N2} {gasto.Moneda}, {gasto.Descripcion}";
    }
}
