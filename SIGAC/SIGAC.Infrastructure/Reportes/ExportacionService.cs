using System.Data;
using System.Drawing;
using FastReport;
using FastReport.Export.PdfSimple;
using FastReport.Utils;
using Microsoft.AspNetCore.Hosting;
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

        public Task<byte[]> ExportarExcelAsync<T>(IEnumerable<T> datos, string titulo)
        {
            // Pendiente a propósito: FastReport.OpenSource (Community) no incluye
            // exportador a XLSX (solo HTML/imagen, y PDF vía el plugin PdfSimple).
            // Generar un .xlsx real necesita otra librería (ej. ClosedXML) además
            // de FastReport, decisión que quedó pendiente de tomar en equipo. El
            // botón de exportar a Excel de la pantalla debe quedar deshabilitado
            // hasta entonces.
            throw new NotImplementedException(
                "Exportación a Excel pendiente: FastReport Community no genera XLSX. Falta elegir la librería a usar.");
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
