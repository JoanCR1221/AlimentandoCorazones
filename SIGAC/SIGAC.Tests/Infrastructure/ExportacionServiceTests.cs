using System.ComponentModel.DataAnnotations;
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

        // Fila con todo lo que el exportador trata aparte: título legible, columna
        // oculta, hora, sí/no y un monto. Clase y no record: [Display] va sobre la
        // propiedad, y en un record posicional habría que escribir [property: ...].
        private sealed class FilaConAtributos
        {
            [Display(AutoGenerateField = false)]
            public int Id { get; set; }

            [Display(Name = "Nombre completo")]
            public string NombreCompleto { get; set; } = string.Empty;

            public TimeSpan Inicio { get; set; }
            public bool Activo { get; set; }
            public decimal Monto { get; set; }
        }

        // El encabezado de la tabla queda siempre en la fila 4: 1 título, 2 período y
        // fecha de generación, 3 en blanco. Los datos arrancan en la 5.
        private const int FilaEncabezado = 4;

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

            Assert.Equal("Nombre", hoja.Cell(FilaEncabezado, 1).GetString());
            Assert.Equal("Fecha", hoja.Cell(FilaEncabezado, 2).GetString());
            Assert.Equal("Monto", hoja.Cell(FilaEncabezado, 3).GetString());

            Assert.Equal("Juan Perez", hoja.Cell(FilaEncabezado + 1, 1).GetString());
            Assert.Equal(datos[0].Fecha.ToString("dd/MM/yyyy"), hoja.Cell(FilaEncabezado + 1, 2).GetString());
            Assert.Equal(datos[0].Monto.ToString("N2"), hoja.Cell(FilaEncabezado + 1, 3).GetString());

            Assert.Equal("Maria Lopez", hoja.Cell(FilaEncabezado + 2, 1).GetString());
            Assert.Equal(datos[1].Fecha.ToString("dd/MM/yyyy"), hoja.Cell(FilaEncabezado + 2, 2).GetString());
            Assert.Equal(datos[1].Monto.ToString("N2"), hoja.Cell(FilaEncabezado + 2, 3).GetString());
        }

        [Fact]
        public async Task Sin_filas_genera_igual_el_encabezado()
        {
            var archivo = await _servicio.ExportarExcelAsync(Array.Empty<FilaDePrueba>(), "Reporte vacio");

            using var libro = new XLWorkbook(new MemoryStream(archivo));
            var hoja = libro.Worksheets.First();

            Assert.Equal("Nombre", hoja.Cell(FilaEncabezado, 1).GetString());
            Assert.True(hoja.Cell(FilaEncabezado + 1, 1).IsEmpty());
        }

        [Fact]
        public async Task Las_columnas_usan_el_titulo_de_Display_y_omiten_las_marcadas_como_ocultas()
        {
            var archivo = await _servicio.ExportarExcelAsync(
                new[] { new FilaConAtributos { Id = 7, NombreCompleto = "Ana", Inicio = new TimeSpan(14, 0, 0), Activo = true, Monto = 10m } },
                "Reporte con atributos");

            using var libro = new XLWorkbook(new MemoryStream(archivo));
            var hoja = libro.Worksheets.First();

            Assert.Equal("Nombre completo", hoja.Cell(FilaEncabezado, 1).GetString());
            Assert.Equal("Inicio", hoja.Cell(FilaEncabezado, 2).GetString());
            Assert.Equal("Activo", hoja.Cell(FilaEncabezado, 3).GetString());
            Assert.Equal("Monto", hoja.Cell(FilaEncabezado, 4).GetString());
            Assert.True(hoja.Cell(FilaEncabezado, 5).IsEmpty());

            // El Id no se exporta: ni como encabezado ni como valor de la fila.
            Assert.DoesNotContain("Id", hoja.Row(FilaEncabezado).CellsUsed().Select(c => c.GetString()));
            Assert.Equal("Ana", hoja.Cell(FilaEncabezado + 1, 1).GetString());
            Assert.DoesNotContain("7", hoja.Row(FilaEncabezado + 1).CellsUsed().Select(c => c.GetString()));
        }

        [Fact]
        public async Task Las_horas_salen_como_hora_y_minutos_y_los_booleanos_como_Si_o_No()
        {
            var archivo = await _servicio.ExportarExcelAsync(
                new[] { new FilaConAtributos { NombreCompleto = "Ana", Inicio = new TimeSpan(14, 30, 0), Activo = false } },
                "Reporte con horas");

            using var libro = new XLWorkbook(new MemoryStream(archivo));
            var hoja = libro.Worksheets.First();

            Assert.Equal("14:30", hoja.Cell(FilaEncabezado + 1, 2).GetString());
            Assert.Equal("No", hoja.Cell(FilaEncabezado + 1, 3).GetString());
        }

        [Fact]
        public async Task El_subtitulo_sale_bajo_el_titulo_junto_a_la_fecha_de_generacion()
        {
            var archivo = await _servicio.ExportarExcelAsync(
                Array.Empty<FilaDePrueba>(), "Reporte de prueba", "Categoría: Todas  ·  Período: todo el historial");

            using var libro = new XLWorkbook(new MemoryStream(archivo));
            var hoja = libro.Worksheets.First();

            Assert.Equal("Reporte de prueba", hoja.Cell(1, 1).GetString());
            Assert.StartsWith("Categoría: Todas  ·  Período: todo el historial", hoja.Cell(2, 1).GetString());
            Assert.Contains("Generado el", hoja.Cell(2, 1).GetString());
        }

        [Fact]
        public async Task Sin_subtitulo_la_segunda_fila_trae_solo_la_fecha_de_generacion()
        {
            var archivo = await _servicio.ExportarExcelAsync(Array.Empty<FilaDePrueba>(), "Reporte de prueba");

            using var libro = new XLWorkbook(new MemoryStream(archivo));

            Assert.StartsWith("Generado el", libro.Worksheets.First().Cell(2, 1).GetString());
        }

        [Fact]
        public async Task El_resumen_se_imprime_bajo_la_tabla_con_una_fila_en_blanco_de_separacion()
        {
            var datos = new[] { new FilaDePrueba("Juan Perez", new DateTime(2026, 3, 15), 100m) };
            var resumen = new[]
            {
                new LineaResumenReporte("Total de asistencias", "12"),
                new LineaResumenReporte("Beneficiarios atendidos", "3")
            };

            var archivo = await _servicio.ExportarExcelAsync(datos, "Reporte de prueba", null, resumen);

            using var libro = new XLWorkbook(new MemoryStream(archivo));
            var hoja = libro.Worksheets.First();

            // Encabezado en la 4, una fila de datos en la 5, en blanco la 6 y el
            // resumen desde la 7.
            Assert.True(hoja.Cell(FilaEncabezado + 2, 1).IsEmpty());
            Assert.Equal("Total de asistencias", hoja.Cell(FilaEncabezado + 3, 1).GetString());
            Assert.Equal("12", hoja.Cell(FilaEncabezado + 3, 2).GetString());
            Assert.Equal("Beneficiarios atendidos", hoja.Cell(FilaEncabezado + 4, 1).GetString());
            Assert.Equal("3", hoja.Cell(FilaEncabezado + 4, 2).GetString());
        }

        [Fact]
        public async Task El_pdf_se_genera_con_subtitulo_resumen_y_columnas_con_atributos()
        {
            var resumen = new[] { new LineaResumenReporte("Total", "12") };

            var pdf = await _servicio.ExportarPDFAsync(
                new[] { new FilaConAtributos { Id = 1, NombreCompleto = "Ana", Inicio = new TimeSpan(9, 0, 0), Activo = true, Monto = 5m } },
                "Reporte de prueba", "Período: todo el historial", resumen);

            // %PDF es la firma de todo archivo PDF: basta para saber que FastReport
            // aceptó los títulos con espacios y el resumen sin romper el enlace de
            // las celdas ([Datos.NombreCompleto] sigue siendo el nombre de la propiedad).
            Assert.True(pdf.Length > 1000);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
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

        [Theory]
        [InlineData("Mesa [grande]")]
        [InlineData("Carpa [Datos.Nombre] 4x4")]
        [InlineData("50% descuento & más {llaves} <b>")]
        [InlineData("Comillas \"dobles\" y 'simples' \\ barra")]
        public async Task El_pdf_acepta_texto_con_corchetes_y_simbolos_en_las_filas_el_subtitulo_y_el_resumen(string texto)
        {
            // Los nombres que escribe el usuario (artículos, sectores, donantes) llegan a
            // las filas, al subtítulo con los filtros y al resumen. FastReport toma lo que
            // va entre corchetes como una expresión: sin escapar, "[grande]" rompe el PDF.
            var filas = new[] { new FilaDePrueba(texto, new DateTime(2026, 9, 1), 5m) };
            var resumen = new[] { new LineaResumenReporte($"Total de {texto}", texto) };

            var pdf = await _servicio.ExportarPDFAsync(filas, $"Reporte de {texto}", $"Artículo: {texto}", resumen);

            Assert.True(pdf.Length > 1000);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        }

        [Theory]
        [InlineData("=1+1")]
        [InlineData("+SUMA(A1:A3)")]
        [InlineData("@usuario")]
        [InlineData("=HYPERLINK(\"http://ejemplo.com\",\"clic\")")]
        public async Task El_excel_deja_como_texto_lo_que_parece_una_formula(string texto)
        {
            // Un donante o un artículo llamado "=1+1" no puede volverse una fórmula al
            // abrir el archivo (inyección de fórmulas): la celda queda como texto.
            var filas = new[] { new FilaDePrueba(texto, new DateTime(2026, 9, 1), 5m) };

            var archivo = await _servicio.ExportarExcelAsync(filas, "Reporte", texto);

            using var libro = new XLWorkbook(new MemoryStream(archivo));
            var hoja = libro.Worksheets.First();
            var celda = hoja.Cell(FilaEncabezado + 1, 1);

            Assert.False(celda.HasFormula, "La celda quedó como fórmula");
            Assert.Equal(XLDataType.Text, celda.DataType);
            Assert.Equal(texto, celda.GetString());
            Assert.False(hoja.Cell(2, 1).HasFormula, "El subtítulo quedó como fórmula");
        }

        [Fact]
        public async Task Un_pdf_de_muchas_hojas_no_pesa_decenas_de_megas()
        {
            // 1 500 filas son unas 70 hojas. PDFSimpleExport escribe cada hoja como una
            // imagen: a su calidad por defecto (300 dpi) serían unos 35 MB, y a la
            // calidad reducida que se usa pasadas las 20 hojas, menos de 12. Y con 4 000
            // filas (más de 100 hojas, el escalón más bajo) el peso por hoja es aún menor.
            var medio = Enumerable.Range(1, 1500)
                .Select(i => new FilaDePrueba($"Persona {i:0000}", new DateTime(2026, 9, 1), i))
                .ToList();
            var largo = Enumerable.Range(1, 4000)
                .Select(i => new FilaDePrueba($"Persona {i:0000}", new DateTime(2026, 9, 1), i))
                .ToList();

            var pdfMedio = await _servicio.ExportarPDFAsync(medio, "Reporte largo");
            var pdfLargo = await _servicio.ExportarPDFAsync(largo, "Reporte muy largo");

            Assert.True(pdfMedio.Length < 20 * 1024 * 1024, $"El PDF de 1 500 filas pesa {pdfMedio.Length / 1024 / 1024} MB");

            // Unas 190 hojas: a 490 KB serían 93 MB; con el escalón más bajo, menos de 25.
            Assert.True(pdfLargo.Length < 25 * 1024 * 1024, $"El PDF de 4 000 filas pesa {pdfLargo.Length / 1024 / 1024} MB");
        }

        // Ancho útil de la hoja carta apaisada con márgenes (ExportacionService.AnchoPagina).
        private const float AnchoHoja = 25.9f;

        private static System.Data.DataTable TablaDeTextos(params (string Titulo, string Valor)[] columnas)
        {
            var tabla = new System.Data.DataTable();

            foreach (var (titulo, _) in columnas)
                tabla.Columns.Add(titulo, typeof(string));

            tabla.Rows.Add(columnas.Select(c => (object)c.Valor).ToArray());
            return tabla;
        }

        [Fact]
        public void El_reparto_de_anchos_llena_exactamente_la_hoja_con_pocas_o_muchas_columnas()
        {
            var pocas = TablaDeTextos(("Nombre", "Ana Mora"), ("Total", "3"));
            var muchas = TablaDeTextos(Enumerable.Range(1, 12).Select(i => ($"Col{i}", new string('x', 40))).ToArray());

            Assert.Equal(AnchoHoja, ExportacionService.AnchosDeColumnas(pocas).Sum(), 3);
            Assert.Equal(AnchoHoja, ExportacionService.AnchosDeColumnas(muchas).Sum(), 3);
        }

        [Fact]
        public void Con_ocho_columnas_las_cortas_conservan_su_ancho_y_ceden_las_de_texto_largo()
        {
            // El reporte de alquileres: fecha, horario, arrendatario, sectores,
            // personas, monto, moneda y estado. Con un reparto proporcional al largo
            // la fecha, el monto y el estado quedaban más angostos que su contenido
            // y se partían en dos líneas.
            var tabla = TablaDeTextos(
                ("Fecha", "09/08/2025"),
                ("Horario", "10:00 a. m. – 12:00 p. m."),
                ("Arrendatario", "Prueba Reporte Asociación Vecinal"),
                ("Sectores", "Área de juego, Cocina (solo para servir)"),
                ("Personas", "40"),
                ("Monto", "999 999,00"),
                ("Moneda", "Colones"),
                ("Estado", "Reservado"));

            var anchos = ExportacionService.AnchosDeColumnas(tabla);

            // Un dígito de Arial 9 pt mide 0,176 cm: diez caracteres y el relleno de
            // la celda necesitan más de 1,9 cm.
            Assert.True(anchos[0] >= 1.9f, $"Fecha: {anchos[0]} cm");
            Assert.True(anchos[5] >= 1.9f, $"Monto: {anchos[5]} cm");
            Assert.True(anchos[7] >= 1.7f, $"Estado: {anchos[7]} cm");
            Assert.Equal(AnchoHoja, anchos.Sum(), 3);

            // Las de texto largo se reparten lo que queda, y siguen siendo las más anchas.
            Assert.True(anchos[3] > anchos[2] && anchos[2] > anchos[0]);
        }

        [Fact]
        public void Si_sobra_hoja_cada_columna_recibe_al_menos_lo_que_necesita_y_las_largas_mas()
        {
            var tabla = TablaDeTextos(
                ("Nombre", "Prueba Reporte Asociación Vecinal"),
                ("Categoría", "Adulto"),
                ("Total", "12"));

            var anchos = ExportacionService.AnchosDeColumnas(tabla);

            Assert.Equal(AnchoHoja, anchos.Sum(), 3);
            Assert.True(anchos[0] > anchos[1] && anchos[1] >= anchos[2]);
        }

        [Fact]
        public void El_reparto_de_anchos_funciona_sin_filas()
        {
            var tabla = new System.Data.DataTable();
            tabla.Columns.Add("Nombre", typeof(string));
            tabla.Columns.Add("Total", typeof(string));

            var anchos = ExportacionService.AnchosDeColumnas(tabla);

            Assert.Equal(2, anchos.Length);
            Assert.Equal(AnchoHoja, anchos.Sum(), 3);
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
