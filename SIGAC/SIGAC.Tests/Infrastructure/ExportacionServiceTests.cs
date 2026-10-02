using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
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
