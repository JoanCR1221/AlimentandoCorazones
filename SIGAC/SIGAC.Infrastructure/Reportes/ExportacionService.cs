using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Reflection;
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
        // Lo que cabe entre márgenes en carta apaisada: 279 mm de papel menos los
        // 10 mm que FastReport deja a cada lado. Con 27 el contenido se pasaba un
        // centímetro del margen derecho y el número de página quedaba cortado.
        private const float AnchoPagina = 25.9f;

        private readonly string? _rutaLogo;

        public ExportacionService(IWebHostEnvironment entorno)
        {
            var candidato = Path.Combine(entorno.WebRootPath, "img", "logo.png");
            _rutaLogo = File.Exists(candidato) ? candidato : null;
        }

        public Task<byte[]> ExportarPDFAsync<T>(IEnumerable<T> datos, string titulo, string? subtitulo = null, IReadOnlyList<LineaResumenReporte>? resumen = null)
        {
            try
            {
                using var report = ConstruirReporte(datos, titulo, subtitulo, resumen);
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
        // ClosedXML. Reutiliza ATabla<T> -y por lo tanto FormatearValor y los
        // títulos de columna- para que una fecha, un monto o un encabezado se vean
        // igual en el PDF y en el Excel del mismo reporte.
        //
        // La hoja se arma como documento y no como tabla pelada: título, línea de
        // período, tabla (encabezado siempre en la fila 4) y el resumen al pie.
        public Task<byte[]> ExportarExcelAsync<T>(IEnumerable<T> datos, string titulo, string? subtitulo = null, IReadOnlyList<LineaResumenReporte>? resumen = null)
        {
            try
            {
                const int filaEncabezado = 4;

                var tabla = ATabla(datos);

                using var libro = new XLWorkbook();
                var hoja = libro.Worksheets.Add(NombreHoja(titulo));

                var celdaTitulo = hoja.Cell(1, 1);
                celdaTitulo.Value = titulo;
                celdaTitulo.Style.Font.Bold = true;
                celdaTitulo.Style.Font.FontSize = 14;

                var celdaSubtitulo = hoja.Cell(2, 1);
                celdaSubtitulo.Value = LineaSubtitulo(subtitulo, DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                celdaSubtitulo.Style.Font.Italic = true;

                for (var columna = 0; columna < tabla.Columns.Count; columna++)
                {
                    var celda = hoja.Cell(filaEncabezado, columna + 1);
                    celda.Value = TituloDe(tabla.Columns[columna]);
                    celda.Style.Font.Bold = true;
                    celda.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                }

                for (var fila = 0; fila < tabla.Rows.Count; fila++)
                {
                    for (var columna = 0; columna < tabla.Columns.Count; columna++)
                        hoja.Cell(filaEncabezado + 1 + fila, columna + 1).Value = (string)tabla.Rows[fila][columna];
                }

                var ultimaFila = filaEncabezado + tabla.Rows.Count;

                if (resumen is { Count: > 0 })
                {
                    // Una fila en blanco de separación, y etiqueta y valor en las dos
                    // primeras columnas: así el resumen cae bajo la tabla y no en una
                    // columna aparte.
                    var fila = ultimaFila + 2;
                    foreach (var linea in resumen)
                    {
                        hoja.Cell(fila, 1).Value = linea.Etiqueta;
                        hoja.Cell(fila, 1).Style.Font.Bold = true;
                        hoja.Cell(fila, 2).Value = linea.Valor;
                        hoja.Cell(fila, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        fila++;
                    }

                    ultimaFila = fila - 1;
                }

                // Solo desde el encabezado hacia abajo: con el título en A1, ajustar
                // toda la hoja ensancharía la primera columna hasta el largo del título.
                hoja.Columns(1, Math.Max(tabla.Columns.Count, 2))
                    .AdjustToContents(filaEncabezado, ultimaFila);

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

        // Mismo contenido que ConstruirReporteGastos, pero fila por fila en una
        // hoja en vez de bandas de FastReport: acá no hace falta resolver
        // paginación, así que alcanza con llevar un cursor de fila y escribir
        // encabezado, grupos (con su "TOTAL . . .") y el "GRAN TOTAL . . ." uno
        // debajo del otro.
        public Task<byte[]> ExportarReporteGastosExcelAsync(ReporteGastosDto reporte, int mes, int anio, string formaPago)
        {
            try
            {
                using var libro = new XLWorkbook();
                var hoja = libro.Worksheets.Add("Gastos");
                var fila = 1;

                hoja.Cell(fila, 1).Value = "ASOCIACION ALIMENTANDO CORAZONES";
                hoja.Cell(fila, 1).Style.Font.Bold = true;
                fila++;

                hoja.Cell(fila, 1).Value = $"DETALLE DE GASTOS MES DE :     {Meses[mes - 1]}     {anio}";
                fila++;

                hoja.Cell(fila, 1).Value = $"POR TIPO DE GASTO Y DESCRIPCION CUENTA :  : GASTOS DE {formaPago.ToUpperInvariant()}";
                fila += 2;

                string[] columnas = { "NOMBRE", "FACT.", "DIA", "MONTO", "I.V.A.", "CHEQUE", "CUENTA CORRIENTE" };

                foreach (var grupo in reporte.Grupos)
                {
                    hoja.Cell(fila, 1).Value = grupo.TipoGasto;
                    hoja.Cell(fila, 1).Style.Font.Bold = true;
                    fila++;

                    hoja.Cell(fila, 1).Value = formaPago.ToUpperInvariant();
                    fila++;

                    hoja.Cell(fila, 1).Value = grupo.DescripcionCuenta;
                    fila++;

                    for (var columna = 0; columna < columnas.Length; columna++)
                    {
                        var celda = hoja.Cell(fila, columna + 1);
                        celda.Value = columnas[columna];
                        celda.Style.Font.Bold = true;
                    }
                    fila++;

                    foreach (var gasto in grupo.Filas)
                    {
                        hoja.Cell(fila, 1).Value = gasto.Proveedor;
                        hoja.Cell(fila, 2).Value = gasto.NumeroFactura;
                        hoja.Cell(fila, 3).Value = gasto.Dia.ToString();
                        hoja.Cell(fila, 4).Value = FormatearMonto(gasto.Monto);
                        hoja.Cell(fila, 5).Value = FormatearMonto(gasto.Iva);
                        hoja.Cell(fila, 6).Value = gasto.NumeroCheque ?? string.Empty;
                        hoja.Cell(fila, 7).Value = gasto.CuentaContable;
                        fila++;
                    }

                    hoja.Cell(fila, 1).Value = "TOTAL . . .";
                    hoja.Cell(fila, 1).Style.Font.Bold = true;
                    hoja.Cell(fila, 4).Value = FormatearMonto(grupo.SubtotalMonto);
                    hoja.Cell(fila, 4).Style.Font.Bold = true;
                    hoja.Cell(fila, 5).Value = FormatearMonto(grupo.SubtotalIva);
                    hoja.Cell(fila, 5).Style.Font.Bold = true;
                    fila += 2;
                }

                hoja.Cell(fila, 1).Value = "GRAN TOTAL . . .";
                hoja.Cell(fila, 1).Style.Font.Bold = true;
                hoja.Cell(fila, 4).Value = FormatearMonto(reporte.GranTotalMonto);
                hoja.Cell(fila, 4).Style.Font.Bold = true;
                hoja.Cell(fila, 5).Value = FormatearMonto(reporte.GranTotalIva);
                hoja.Cell(fila, 5).Style.Font.Bold = true;

                hoja.Columns().AdjustToContents();

                using var salida = new MemoryStream();
                libro.SaveAs(salida);
                return Task.FromResult(salida.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception("Error al exportar el reporte de gastos a Excel.", ex);
            }
        }

        // "Período: ... · Generado el ..." o solo "Generado el ..." si el reporte no
        // trae período. Igual en el PDF y en el Excel.
        private static string LineaSubtitulo(string? subtitulo, string generado) =>
            string.IsNullOrWhiteSpace(subtitulo)
                ? $"Generado el {generado}"
                : $"{subtitulo}  ·  Generado el {generado}";

        // Arma un Report en memoria: encabezado con logo y título, una fila de
        // rótulos, una DataBand con una celda por propiedad pública de T y, si hay
        // resumen, un pie de página con los totales.
        private Report ConstruirReporte<T>(IEnumerable<T> datos, string titulo, string? subtitulo, IReadOnlyList<LineaResumenReporte>? resumen)
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

            var anchos = AnchosDeColumnas(tabla);

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
                    // Termina antes de la fila de encabezados de columna (que arranca
                    // 0.6 cm sobre el borde inferior): con -0.4 el logo la tocaba.
                    Bounds = new RectangleF(Cm(0), Cm(0), Cm(AnchoLogo), Cm(AltoEncabezado - 0.7f)),
                    ImageLocation = _rutaLogo
                });
            }

            // 4.2 cm menos de ancho: ese espacio de la derecha es del número de página.
            encabezado.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(AnchoLogo + 0.3f), Cm(0.2f), Cm(AnchoPagina - AnchoLogo - 0.3f - 4.2f), Cm(0.9f)),
                Text = titulo,
                Font = new Font("Arial", 14, FontStyle.Bold)
            });

            AgregarNumeroPagina(encabezado);

            encabezado.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(Cm(AnchoLogo + 0.3f), Cm(1.1f), Cm(AnchoPagina - AnchoLogo - 0.3f), Cm(0.6f)),
                Text = LineaSubtitulo(subtitulo, "[Date]"),
                Font = new Font("Arial", 8, FontStyle.Italic)
            });

            var x = 0f;
            for (var i = 0; i < tabla.Columns.Count; i++)
            {
                encabezado.Objects.Add(new TextObject
                {
                    Bounds = new RectangleF(Cm(x), Cm(AltoEncabezado - 0.6f), Cm(anchos[i]), Cm(0.6f)),
                    // El título legible y no ColumnName: el nombre es la propiedad y
                    // es lo que enlaza [Datos.X] más abajo.
                    Text = TituloDe(tabla.Columns[i]),
                    Font = new Font("Arial", 9, FontStyle.Bold),
                    Border = { Lines = FastReport.BorderLines.Bottom }
                });
                x += anchos[i];
            }

            // CanGrow en la banda y en cada celda: un nombre que no entra en el
            // ancho de su columna se parte en dos líneas (WordWrap, por defecto en
            // TextObject), y sin esto la fila no crece para darle espacio, asi que
            // la segunda línea se dibuja encima de la fila siguiente en vez de
            // empujarla hacia abajo.
            var filas = new DataBand
            {
                Height = Cm(AltoFila),
                DataSource = origen,
                CanGrow = true
            };
            pagina.Bands.Add(filas);

            x = 0f;
            for (var i = 0; i < tabla.Columns.Count; i++)
            {
                filas.Objects.Add(new TextObject
                {
                    // 0.1 cm de margen arriba: sin él el texto de la primera fila se
                    // pegaba a la línea bajo los encabezados.
                    Bounds = new RectangleF(Cm(x), Cm(0.1f), Cm(anchos[i]), Cm(AltoFila - 0.1f)),
                    Text = $"[Datos.{tabla.Columns[i].ColumnName}]",
                    Font = new Font("Arial", 9),
                    CanGrow = true
                });
                x += anchos[i];
            }

            if (resumen is { Count: > 0 })
            {
                const float anchoEtiqueta = 9f;
                const float altoLinea = 0.55f;

                // Pie de página y no ReportSummary: el resumen queda siempre abajo de
                // la hoja, también cuando la tabla es corta, y se repite en cada
                // hoja si el reporte ocupa varias. FastReport reserva su alto antes
                // de repartir las filas, así que nunca se pisa con los datos.
                var pie = new PageFooterBand { Height = Cm(0.4f + resumen.Count * altoLinea) };
                pagina.PageFooter = pie;

                pie.Objects.Add(new LineObject
                {
                    Bounds = new RectangleF(Cm(0), Cm(0.05f), Cm(AnchoPagina), 0)
                });

                for (var i = 0; i < resumen.Count; i++)
                {
                    var y = 0.2f + i * altoLinea;

                    pie.Objects.Add(new TextObject
                    {
                        Bounds = new RectangleF(Cm(0), Cm(y), Cm(anchoEtiqueta), Cm(altoLinea)),
                        Text = resumen[i].Etiqueta,
                        Font = new Font("Arial", 9, FontStyle.Bold)
                    });
                    pie.Objects.Add(new TextObject
                    {
                        Bounds = new RectangleF(Cm(anchoEtiqueta + 0.2f), Cm(y), Cm(AnchoPagina - anchoEtiqueta - 0.2f), Cm(altoLinea)),
                        Text = resumen[i].Valor,
                        Font = new Font("Arial", 9)
                    });
                }
            }

            // Necesario para que [TotalPages] ("Página 1 de 3") sepa el total desde la
            // primera hoja: FastReport arma el reporte dos veces.
            report.DoublePass = true;
            report.Prepare();
            return report;
        }

        // Reparte AnchoPagina entre las columnas según lo largo de su contenido (el
        // título o el valor más largo), no en partes iguales: un nombre necesita
        // varias veces el ancho de un contador, y con partes iguales el nombre se
        // partía en dos líneas mientras los números sobraban espacio. El mínimo y el
        // máximo evitan que una columna de una letra desaparezca o que una de
        // observaciones se coma la hoja.
        private static float[] AnchosDeColumnas(DataTable tabla)
        {
            var pesos = tabla.Columns.Cast<DataColumn>()
                .Select(c =>
                {
                    var mayor = tabla.Rows.Cast<DataRow>()
                        .Select(f => ((string)f[c]).Length)
                        .DefaultIfEmpty(0)
                        .Max();

                    return (float)Math.Clamp(Math.Max(mayor, TituloDe(c).Length), 8, 40);
                })
                .ToArray();

            var total = Math.Max(pesos.Sum(), 1f);
            return pesos.Select(p => AnchoPagina * p / total).ToArray();
        }

        // "Página 1 de 3" arriba a la derecha de cada hoja. El ancho cabe en una
        // línea de 8 pt hasta "Página 99 de 99".
        private static void AgregarNumeroPagina(PageHeaderBand encabezado)
        {
            const float ancho = 4f;

            encabezado.Objects.Add(new TextObject
            {
                Bounds = new RectangleF(
                    (AnchoPagina - ancho) * Units.Centimeters,
                    0.2f * Units.Centimeters,
                    ancho * Units.Centimeters,
                    0.5f * Units.Centimeters),
                Text = "Página [Page] de [TotalPages]",
                HorzAlign = HorzAlign.Right,
                Font = new Font("Arial", 8)
            });
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

            // Proporciones pensadas sobre 27 cm; se reparten sobre lo que de verdad
            // cabe entre márgenes para que la última columna no se salga de la hoja.
            var escala = AnchoPagina / anchos.Sum();
            anchos = anchos.Select(a => a * escala).ToArray();

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

            AgregarNumeroPagina(encabezado);

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

            // CanGrow por la misma razón que en ConstruirReporte<T>: un proveedor
            // con nombre largo se envuelve a dos líneas y, sin esto, la segunda
            // línea se superpone con la fila siguiente en vez de empujarla.
            var filas = new DataBand { Height = Cm(altoFila), DataSource = origen, CanGrow = true };
            grupoHeader.Data = filas;

            x = 0f;
            for (var i = 0; i < campos.Length; i++)
            {
                filas.Objects.Add(new TextObject
                {
                    Bounds = new RectangleF(Cm(x), Cm(0), Cm(anchos[i]), Cm(altoFila)),
                    Text = $"[Datos.{campos[i]}]",
                    Font = new Font("Arial", 8),
                    CanGrow = true
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

            report.DoublePass = true;
            report.Prepare();
            return report;
        }

        // InvariantCulture a propósito y no la cultura del servidor: el reporte
        // reproduce el formato exacto del documento de la contadora (1,234.56,
        // separador de miles con coma y decimal con punto) sin importar en qué
        // configuración regional corra el servidor.
        private static string FormatearMonto(decimal monto) => monto.ToString("N2", CultureInfo.InvariantCulture);

        // Una columna por propiedad pública de T, en el orden en que se declaran.
        // T ya es el DTO de fila del reporte (ReporteBeneficiariosDto y los que
        // sigan), armado para mostrarse tal cual, no la entidad completa.
        //
        // Dos atributos de DataAnnotations afinan la exportación sin afectar a la
        // pantalla: [Display(Name)] da el título de la columna y
        // [Display(AutoGenerateField = false)] deja la propiedad fuera (un Id que
        // la grilla necesita pero el papel no).
        //
        // El título va en ExtendedProperties y no en ColumnName ni en Caption: el
        // nombre tiene que seguir siendo el de la propiedad porque FastReport enlaza
        // cada celda como [Datos.Nombre], y Caption lo toma como alias de la columna
        // (confirmado con una prueba: un título con espacios dejaba la expresión
        // sin resolver y el PDF fallaba con "The name 'Datos' does not exist").
        private const string ClaveTitulo = "Titulo";

        private static string TituloDe(DataColumn columna) =>
            columna.ExtendedProperties[ClaveTitulo] as string ?? columna.ColumnName;

        private static DataTable ATabla<T>(IEnumerable<T> datos)
        {
            var tabla = new DataTable();
            var propiedades = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<DisplayAttribute>()?.GetAutoGenerateField() != false)
                .ToArray();

            foreach (var propiedad in propiedades)
            {
                var columna = tabla.Columns.Add(propiedad.Name, typeof(string));
                columna.ExtendedProperties[ClaveTitulo] = propiedad.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? propiedad.Name;
            }

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
            // 24 horas y sin segundos: sin esto una hora saldría como 10:00:00.
            TimeSpan hora => hora.ToString(@"hh\:mm"),
            bool si => si ? "Sí" : "No",
            decimal monto => monto.ToString("N2"),
            _ => valor.ToString() ?? string.Empty
        };
    }
}
