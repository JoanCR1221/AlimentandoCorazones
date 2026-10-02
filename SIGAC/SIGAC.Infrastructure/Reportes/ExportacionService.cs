using System.Data;
using System.Drawing;
using System.Globalization;
using ClosedXML.Excel;
using FastReport;
using FastReport.Export.PdfSimple;
using FastReport.Utils;
using Microsoft.AspNetCore.Hosting;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Interfaces;

namespace SIGAC.Infrastructure.Reportes
{
    // Exportación genérica a PDF y Excel para el módulo de Generación de reportes,
    // sobre FastReport.OpenSource (Community). No hay una plantilla .frx por
    // reporte: la página se arma en memoria a partir de las propiedades públicas
    // de T, así que agregar un reporte nuevo (Donaciones, Inventario, Gastos) no
    // toca esta clase.
    public class ExportacionService : IExportacionService
    {
        // Medidas en centímetros, unidad nativa de FastReport para Bounds.
        private const float AltoEncabezado = 2.2f;
        private const float AltoFila = 0.7f;
        private const float AnchoLogo = 2.2f;
        private const float AnchoPagina = 27f; // Carta apaisada, con margen

        private readonly string? _rutaLogo;

        public ExportacionService(IWebHostEnvironment entorno)
        {
            var candidato = Path.Combine(entorno.WebRootPath, "img", "logo.png");
            _rutaLogo = File.Exists(candidato) ? candidato : null;
        }

        public Task<byte[]> ExportarPDFAsync<T>(IEnumerable<T> datos, string titulo)
        {
            try
            {
                using var report = ConstruirReporte(datos, titulo);
                using var export = new PDFSimpleExport();
                using var salida = new MemoryStream();

                report.Export(export, salida);
                return Task.FromResult(salida.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception("Error al exportar el reporte a PDF.", ex);
            }
        }

        // FastReport.OpenSource (Community) no incluye exportador a XLSX (solo
        // HTML/imagen, y PDF vía el plugin PdfSimple), así que esto va aparte con
        // ClosedXML. Reutiliza ATabla<T> -y por lo tanto FormatearValor- para que
        // una fecha o un monto se vean igual en el PDF y en el Excel del mismo
        // reporte.
        public Task<byte[]> ExportarExcelAsync<T>(IEnumerable<T> datos, string titulo)
        {
            try
            {
                var tabla = ATabla(datos);

                using var libro = new XLWorkbook();
                var hoja = libro.Worksheets.Add(NombreHoja(titulo));

                for (var columna = 0; columna < tabla.Columns.Count; columna++)
                {
                    var celda = hoja.Cell(1, columna + 1);
                    celda.Value = tabla.Columns[columna].ColumnName;
                    celda.Style.Font.Bold = true;
                }

                for (var fila = 0; fila < tabla.Rows.Count; fila++)
                {
                    for (var columna = 0; columna < tabla.Columns.Count; columna++)
                        hoja.Cell(fila + 2, columna + 1).Value = (string)tabla.Rows[fila][columna];
                }

                hoja.Columns().AdjustToContents();

                using var salida = new MemoryStream();
                libro.SaveAs(salida);
                return Task.FromResult(salida.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception("Error al exportar el reporte a Excel.", ex);
            }
        }

        // El título del reporte ("Reporte de beneficiarios atendidos") es el
        // nombre de hoja más natural, pero Excel limita a 31 caracteres y
        // prohíbe : \ / ? * [ ], cosas que un título en español nunca respeta
        // por diseño.
        private static string NombreHoja(string titulo)
        {
            var sinInvalidos = string.Concat(titulo.Select(c => "\\/?*[]:".Contains(c) ? ' ' : c));
            return sinInvalidos.Length > 31 ? sinInvalidos[..31] : sinInvalidos;
        }

        public Task<byte[]> ExportarReporteGastosPDFAsync(ReporteGastosDto reporte, int mes, int anio, string formaPago)
        {
            try
            {
                using var report = ConstruirReporteGastos(reporte, mes, anio, formaPago);
                using var export = new PDFSimpleExport();
                using var salida = new MemoryStream();

                report.Export(export, salida);
                return Task.FromResult(salida.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception("Error al exportar el reporte de gastos a PDF.", ex);
            }
        }

        // Arma un Report en memoria: encabezado con logo y título, una fila de
        // rótulos y una DataBand con una celda por propiedad pública de T.
        private Report ConstruirReporte<T>(IEnumerable<T> datos, string titulo)
        {
            // Bounds y Height se miden en la unidad interna de FastReport
            // (~96 por pulgada), no en centímetros directos: todo valor en cm pasa
            // por Cm() antes de usarse. Confirmado con un PDF de prueba real: sin
            // esta conversión, los objetos quedan del tamaño de un pixel.
            static float Cm(float centimetros) => centimetros * Units.Centimeters;

            var tabla = ATabla(datos);

            var report = new Report();
            var pagina = new ReportPage
            {
                Name = "Pagina1",
                // PaperWidth/PaperHeight sí van en milímetros (convención de
                // FastReport para tamaño de papel, no la escala de Bounds). Carta
                // apaisada: 279 x 216 mm.
                PaperWidth = 279f,
                PaperHeight = 216f
            };
            report.Pages.Add(pagina);

            report.RegisterData(tabla, "Datos");
            var origen = report.GetDataSource("Datos")!;
            origen.Enabled = true;

            var anchoColumna = AnchoPagina / Math.Max(tabla.Columns.Count, 1);

            // Asignación a la propiedad nombrada y no Bands.Add: FastReport solo
            // imprime PageHeaderBand/DataBand si están enganchados a las
            // propiedades PageHeader/Bands correspondientes de ReportPage;
            // agregarlo solo a la colección genérica lo deja sin dibujar.
            var encabezado = new PageHeaderBand { Height = Cm(AltoEncabezado) };
            pagina.PageHeader = encabezado;

            if (_rutaLogo is not null)
            {
                encabezado.Objects.Add(new PictureObject
                {
                    Bounds = new RectangleF(Cm(0), Cm(0), Cm(AnchoLogo), Cm(AltoEncabezado - 0.4f)),
                    ImageLocation = _rutaLogo
                });
            }

            encabezado.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(AnchoLogo + 0.3f), Cm(0.2f), Cm(AnchoPagina - AnchoLogo - 0.3f), Cm(0.9f)),
                Text = titulo,
                Font = new Font("Arial", 14, FontStyle.Bold)
            });

            encabezado.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(AnchoLogo + 0.3f), Cm(1.1f), Cm(AnchoPagina - AnchoLogo - 0.3f), Cm(0.6f)),
                Text = "Generado el [Date]",
                Font = new Font("Arial", 8, FontStyle.Italic)
            });

            var x = 0f;
            foreach (DataColumn columna in tabla.Columns)
            {
                encabezado.Objects.Add(new TextObject
                {
                    Bounds = new RectangleF(Cm(x), Cm(AltoEncabezado - 0.6f), Cm(anchoColumna), Cm(0.6f)),
                    Text = columna.ColumnName,
                    Font = new Font("Arial", 9, FontStyle.Bold),
                    Border = { Lines = FastReport.BorderLines.Bottom }
                });
                x += anchoColumna;
            }

            var filas = new DataBand
            {
                Height = Cm(AltoFila),
                DataSource = origen
            };
            pagina.Bands.Add(filas);

            x = 0f;
            foreach (DataColumn columna in tabla.Columns)
            {
                filas.Objects.Add(new TextObject
                {
                    Bounds = new RectangleF(Cm(x), Cm(0), Cm(anchoColumna), Cm(AltoFila)),
                    Text = $"[Datos.{columna.ColumnName}]",
                    Font = new Font("Arial", 9)
                });
                x += anchoColumna;
            }

            report.Prepare();
            return report;
        }

        // Nombres de mes en mayúscula para el encabezado ("DETALLE DE GASTOS MES
        // DE : SETIEMBRE 2026"), igual que el reporte de la contadora. "Setiembre"
        // y no "Septiembre": así lo escribe el documento original.
        private static readonly string[] Meses =
        {
            "ENERO", "FEBRERO", "MARZO", "ABRIL", "MAYO", "JUNIO",
            "JULIO", "AGOSTO", "SETIEMBRE", "OCTUBRE", "NOVIEMBRE", "DICIEMBRE"
        };

        // Arma el reporte contable de gastos: mismo formato que usa hoy la
        // contadora, agrupado por tipo de gasto y descripción de cuenta, con
        // subtotal por grupo y un gran total. A diferencia de ConstruirReporte<T>,
        // acá las filas no son una tabla plana: se usan bandas de grupo de
        // FastReport (GroupHeaderBand/GroupFooterBand) porque la agrupación y el
        // salto de página entre grupos los tiene que resolver FastReport, no un
        // cálculo manual de posiciones en C#.
        //
        // Los datos ya llegan agrupados y ordenados desde ReportesService: acá
        // alcanza con aplanarlos a una sola tabla (una fila por gasto, con el tipo
        // y el subtotal de su grupo repetidos en cada fila) y dejar que
        // GroupHeaderBand.Condition detecte el cambio de grupo fila a fila.
        private Report ConstruirReporteGastos(ReporteGastosDto reporte, int mes, int anio, string formaPago)
        {
            static float Cm(float centimetros) => centimetros * Units.Centimeters;

            const float altoEncabezado = 2.6f;
            const float altoGrupoHeader = 1.2f;
            const float altoFila = 0.6f;
            const float altoGrupoFooter = 0.5f;
            const float altoResumen = 0.6f;

            var tabla = new DataTable();
            foreach (var columna in new[] { "TipoGasto", "DescripcionCuenta", "Proveedor", "Fact", "Dia", "Monto", "Iva", "Cheque", "CuentaContable", "SubtotalMonto", "SubtotalIva" })
                tabla.Columns.Add(columna, typeof(string));

            foreach (var grupo in reporte.Grupos)
            {
                foreach (var fila in grupo.Filas)
                {
                    tabla.Rows.Add(
                        grupo.TipoGasto, grupo.DescripcionCuenta, fila.Proveedor, fila.NumeroFactura, fila.Dia.ToString(),
                        FormatearMonto(fila.Monto), FormatearMonto(fila.Iva), fila.NumeroCheque ?? string.Empty, fila.CuentaContable,
                        FormatearMonto(grupo.SubtotalMonto), FormatearMonto(grupo.SubtotalIva));
                }
            }

            var report = new Report();
            var pagina = new ReportPage
            {
                Name = "Pagina1",
                PaperWidth = 279f,
                PaperHeight = 216f
            };
            report.Pages.Add(pagina);

            report.RegisterData(tabla, "Datos");
            var origen = report.GetDataSource("Datos")!;
            origen.Enabled = true;

            // Mismo ancho de columnas en el encabezado, las filas y los totales:
            // si se desalinean entre bandas, las cifras no caen debajo de su
            // columna.
            string[] columnas = { "NOMBRE", "FACT.", "DIA", "MONTO", "I.V.A.", "CHEQUE", "CUENTA CORRIENTE" };
            string[] campos = { "Proveedor", "Fact", "Dia", "Monto", "Iva", "Cheque", "CuentaContable" };
            float[] anchos = { 7f, 3f, 2f, 4f, 4f, 3f, 4f };
            var anchoColumnaMonto = anchos[3];
            var anchoColumnaIva = anchos[4];
            var xColumnaMonto = anchos[0] + anchos[1] + anchos[2];
            var xColumnaIva = xColumnaMonto + anchoColumnaMonto;

            var encabezado = new PageHeaderBand { Height = Cm(altoEncabezado) };
            pagina.PageHeader = encabezado;

            encabezado.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(0), Cm(0), Cm(AnchoPagina), Cm(0.5f)),
                Text = "ASOCIACION ALIMENTANDO CORAZONES",
                Font = new Font("Arial", 11, FontStyle.Bold)
            });
            encabezado.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(0), Cm(0.6f), Cm(AnchoPagina), Cm(0.5f)),
                Text = $"DETALLE DE GASTOS MES DE :     {Meses[mes - 1]}     {anio}",
                Font = new Font("Arial", 9)
            });
            encabezado.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(0), Cm(1.1f), Cm(AnchoPagina), Cm(0.5f)),
                Text = $"POR TIPO DE GASTO Y DESCRIPCION CUENTA :  : GASTOS DE {formaPago.ToUpperInvariant()}",
                Font = new Font("Arial", 9)
            });

            var x = 0f;
            for (var i = 0; i < columnas.Length; i++)
            {
                encabezado.Objects.Add(new TextObject
                {
                    Bounds = new RectangleF(Cm(x), Cm(altoEncabezado - 0.6f), Cm(anchos[i]), Cm(0.5f)),
                    Text = columnas[i],
                    Font = new Font("Arial", 8, FontStyle.Bold),
                    Border = { Lines = FastReport.BorderLines.Bottom }
                });
                x += anchos[i];
            }

            // Un grupo por cada combinación (TipoGasto, DescripcionCuenta) que ya
            // trae ReporteGastosDto: como la tabla llega pre-ordenada por grupo,
            // a FastReport le alcanza con detectar cuándo cambia TipoGasto fila a
            // fila para abrir uno nuevo.
            var grupoHeader = new GroupHeaderBand
            {
                Height = Cm(altoGrupoHeader),
                Condition = "[Datos.TipoGasto]"
            };
            pagina.Bands.Add(grupoHeader);

            grupoHeader.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(0), Cm(0), Cm(AnchoPagina), Cm(0.4f)),
                Text = "[Datos.TipoGasto]",
                Font = new Font("Arial", 9, FontStyle.Bold)
            });
            grupoHeader.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(0), Cm(0.4f), Cm(AnchoPagina), Cm(0.4f)),
                // Dato fijo y no ligado a la fila: la forma de pago es el filtro de
                // todo el reporte, no una columna que cambie entre grupos.
                Text = formaPago.ToUpperInvariant(),
                Font = new Font("Arial", 8)
            });
            grupoHeader.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(0), Cm(0.8f), Cm(AnchoPagina), Cm(0.4f)),
                Text = "[Datos.DescripcionCuenta]",
                Font = new Font("Arial", 8)
            });

            var filas = new DataBand { Height = Cm(altoFila), DataSource = origen };
            grupoHeader.Data = filas;

            x = 0f;
            for (var i = 0; i < campos.Length; i++)
            {
                filas.Objects.Add(new TextObject
                {
                    Bounds = new RectangleF(Cm(x), Cm(0), Cm(anchos[i]), Cm(altoFila)),
                    Text = $"[Datos.{campos[i]}]",
                    Font = new Font("Arial", 8)
                });
                x += anchos[i];
            }

            grupoHeader.GroupFooter = new GroupFooterBand { Height = Cm(altoGrupoFooter) };
            grupoHeader.GroupFooter.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(0), Cm(0), Cm(6f), Cm(altoGrupoFooter)),
                Text = "TOTAL . . .",
                Font = new Font("Arial", 8, FontStyle.Bold)
            });
            grupoHeader.GroupFooter.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(xColumnaMonto), Cm(0), Cm(anchoColumnaMonto), Cm(altoGrupoFooter)),
                Text = "[Datos.SubtotalMonto]",
                Font = new Font("Arial", 8, FontStyle.Bold)
            });
            grupoHeader.GroupFooter.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(xColumnaIva), Cm(0), Cm(anchoColumnaIva), Cm(altoGrupoFooter)),
                Text = "[Datos.SubtotalIva]",
                Font = new Font("Arial", 8, FontStyle.Bold)
            });

            // Una sola vez, al final de todo el reporte (no por página ni por
            // grupo): FastReport lo imprime después de la última fila de datos.
            pagina.ReportSummary = new ReportSummaryBand { Height = Cm(altoResumen) };
            pagina.ReportSummary.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(0), Cm(0), Cm(6f), Cm(altoResumen)),
                Text = "GRAN TOTAL . . .",
                Font = new Font("Arial", 9, FontStyle.Bold)
            });
            pagina.ReportSummary.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(xColumnaMonto), Cm(0), Cm(anchoColumnaMonto), Cm(altoResumen)),
                Text = FormatearMonto(reporte.GranTotalMonto),
                Font = new Font("Arial", 9, FontStyle.Bold)
            });
            pagina.ReportSummary.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(xColumnaIva), Cm(0), Cm(anchoColumnaIva), Cm(altoResumen)),
                Text = FormatearMonto(reporte.GranTotalIva),
                Font = new Font("Arial", 9, FontStyle.Bold)
            });

            report.Prepare();
            return report;
        }

        // InvariantCulture a propósito y no la cultura del servidor: el reporte
        // reproduce el formato exacto del documento de la contadora (1,234.56,
        // separador de miles con coma y decimal con punto) sin importar en qué
        // configuración regional corra el servidor.
        private static string FormatearMonto(decimal monto) => monto.ToString("N2", CultureInfo.InvariantCulture);

        // Una columna por propiedad pública de T, en el orden en que se declaran.
        // Sin atributos de exclusión a propósito: T ya es el DTO de fila del
        // reporte (ReporteBeneficiariosDto y los que sigan), armado para mostrarse
        // tal cual, no la entidad completa.
        private static DataTable ATabla<T>(IEnumerable<T> datos)
        {
            var tabla = new DataTable();
            var propiedades = typeof(T).GetProperties();

            foreach (var propiedad in propiedades)
                tabla.Columns.Add(propiedad.Name, typeof(string));

            foreach (var fila in datos)
            {
                var valores = propiedades
                    .Select(p => FormatearValor(p.GetValue(fila)))
                    .ToArray();

                tabla.Rows.Add(valores);
            }

            return tabla;
        }

        private static string FormatearValor(object? valor) => valor switch
        {
            null => string.Empty,
            DateTime fecha => fecha.ToString("dd/MM/yyyy"),
            decimal monto => monto.ToString("N2"),
            _ => valor.ToString() ?? string.Empty
        };
    }
}
