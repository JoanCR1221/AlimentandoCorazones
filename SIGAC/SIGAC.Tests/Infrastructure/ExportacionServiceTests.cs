using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Infrastructure.Reportes;

namespace SIGAC.Tests.Infrastructure
{
    public class ExportacionServiceTests
    {
        // Nombre, fecha y monto a proposito: son los dos casos que
        // FormatearValor formatea distinto del resto (dd/MM/yyyy y N2), y que
        // el reporte de beneficiarios ya exporta a PDF con el mismo criterio.
        private sealed record FilaDePrueba(string Nombre, DateTime Fecha, decimal Monto);

        private readonly ExportacionService _servicio = new(new EntornoFalso());

        [Fact]
        public async Task Genera_el_encabezado_y_las_filas_con_el_mismo_formato_que_FormatearValor()
        {
            var datos = new[]
            {
                new FilaDePrueba("Juan Perez", new DateTime(2026, 3, 15), 1234.5m),
                new FilaDePrueba("Maria Lopez", new DateTime(2026, 4, 2), 987.25m)
            };

            var archivo = await _servicio.ExportarExcelAsync(datos, "Reporte de prueba");

            using var libro = new XLWorkbook(new MemoryStream(archivo));
            var hoja = libro.Worksheets.First();

            Assert.Equal("Nombre", hoja.Cell(1, 1).GetString());
            Assert.Equal("Fecha", hoja.Cell(1, 2).GetString());
            Assert.Equal("Monto", hoja.Cell(1, 3).GetString());

            Assert.Equal("Juan Perez", hoja.Cell(2, 1).GetString());
            Assert.Equal(datos[0].Fecha.ToString("dd/MM/yyyy"), hoja.Cell(2, 2).GetString());
            Assert.Equal(datos[0].Monto.ToString("N2"), hoja.Cell(2, 3).GetString());

            Assert.Equal("Maria Lopez", hoja.Cell(3, 1).GetString());
            Assert.Equal(datos[1].Fecha.ToString("dd/MM/yyyy"), hoja.Cell(3, 2).GetString());
            Assert.Equal(datos[1].Monto.ToString("N2"), hoja.Cell(3, 3).GetString());
        }

        [Fact]
        public async Task Sin_filas_genera_igual_el_encabezado()
        {
            var archivo = await _servicio.ExportarExcelAsync(Array.Empty<FilaDePrueba>(), "Reporte vacio");

            using var libro = new XLWorkbook(new MemoryStream(archivo));
            var hoja = libro.Worksheets.First();

            Assert.Equal("Nombre", hoja.Cell(1, 1).GetString());
            Assert.True(hoja.Cell(2, 1).IsEmpty());
        }

        [Fact]
        public async Task Un_titulo_largo_o_con_caracteres_invalidos_no_rompe_el_nombre_de_la_hoja()
        {
            var archivo = await _servicio.ExportarExcelAsync(
                Array.Empty<FilaDePrueba>(), "Reporte: beneficiarios/donantes [activos] * 2026");

            using var libro = new XLWorkbook(new MemoryStream(archivo));

            Assert.True(libro.Worksheets.First().Name.Length <= 31);
        }

        // Dos grupos a proposito: hay que ver que cada uno cierre con su propio
        // "TOTAL . . ." y que el "GRAN TOTAL . . ." final sume los dos, no solo
        // el ultimo.
        [Fact]
        public async Task El_reporte_de_gastos_imprime_cada_grupo_con_su_total_y_el_gran_total_al_final()
        {
            var reporte = new ReporteGastosDto
            {
                Grupos = new List<GrupoReporteGastosDto>
                {
                    new()
                    {
                        TipoGasto = "Alquiler de Equipo",
                        DescripcionCuenta = "GASTOS ADMINISTRATIVOS",
                        Filas = new List<FilaReporteGastosDto>
                        {
                            new("Renta Equipos S.A.", "76216", 1, 25175.97m, 3272.88m, null, "1 CAJA Y BANCOS")
                        },
                        SubtotalMonto = 25175.97m,
                        SubtotalIva = 3272.88m
                    },
                    new()
                    {
                        TipoGasto = "Servicio de Agua",
                        DescripcionCuenta = "GASTOS ADMINISTRATIVOS",
                        Filas = new List<FilaReporteGastosDto>
                        {
                            new("AyA", "4863", 2, 23798.40m, 2758.60m, "001", "1 CAJA Y BANCOS")
                        },
                        SubtotalMonto = 23798.40m,
                        SubtotalIva = 2758.60m
                    }
                },
                GranTotalMonto = 48974.37m,
                GranTotalIva = 6031.48m
            };

            var archivo = await _servicio.ExportarReporteGastosExcelAsync(reporte, 9, 2026, "Contado");

            using var libro = new XLWorkbook(new MemoryStream(archivo));
            var texto = string.Join('\n', libro.Worksheets.First().CellsUsed().Select(c => c.GetString()));

            Assert.Contains("DETALLE DE GASTOS MES DE :     SETIEMBRE     2026", texto);
            Assert.Contains("GASTOS DE CONTADO", texto);
            Assert.Contains("Alquiler de Equipo", texto);
            Assert.Contains("Renta Equipos S.A.", texto);
            Assert.Contains("25,175.97", texto);
            Assert.Contains("Servicio de Agua", texto);
            Assert.Contains("AyA", texto);
            Assert.Contains("23,798.40", texto);
            Assert.Contains("48,974.37", texto);
            Assert.Contains("6,031.48", texto);
        }

        [Fact]
        public async Task El_reporte_de_gastos_sin_grupos_igual_imprime_el_encabezado_y_el_gran_total_en_cero()
        {
            var archivo = await _servicio.ExportarReporteGastosExcelAsync(new ReporteGastosDto(), 9, 2026, "Contado");

            using var libro = new XLWorkbook(new MemoryStream(archivo));
            var texto = string.Join('\n', libro.Worksheets.First().CellsUsed().Select(c => c.GetString()));

            Assert.Contains("ASOCIACION ALIMENTANDO CORAZONES", texto);
            Assert.Contains("GRAN TOTAL . . .", texto);
            Assert.Contains("0.00", texto);
        }

        private sealed class EntornoFalso : IWebHostEnvironment
        {
            public string WebRootPath { get; set; } = AppContext.BaseDirectory;
            public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
            public string ApplicationName { get; set; } = "SIGAC.Tests";
            public string EnvironmentName { get; set; } = "Development";
        }
    }
}
