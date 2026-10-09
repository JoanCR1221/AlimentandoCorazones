using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // El reporte de gastos, los de donaciones, alquileres, inventario y proyectos,
    // los panoramas de esos cuatro (lo que hace el servicio con lo que le devuelve
    // el repositorio) y la agrupación del de beneficiarios. Las consultas SQL de los
    // panoramas, los otros dos paneles gráficos y el resto del de beneficiarios
    // están cubiertos por verificación manual en el navegador (ver las notas de los
    // commits que los agregaron).
    public class ReportesServiceTests
    {
        private readonly RepositorioTiposGastoFalso _tipos = new();
        private readonly RepositorioGastosFalso _gastos;
        private readonly RepositorioAsistenciaFalso _asistencias = new();
        private readonly RepositorioDonacionesFalso _donaciones = new();
        private readonly RepositorioDonantesFalso _donantes = new();
        private readonly RepositorioAlquileresFalso _alquileres = new();
        private readonly RepositorioInventarioFalso _inventario = new();
        private readonly RepositorioProyectosFalso _proyectos = new();
        private readonly ReportesService _servicio;

        private readonly TipoGasto _alquiler;
        private readonly TipoGasto _combustible;

        public ReportesServiceTests()
        {
            _gastos = new RepositorioGastosFalso(_tipos);

            // El reporte de donaciones solo usa el historial del DonacionesService
            // real (así las pruebas cubren el filtro por tipo y el total por moneda
            // de verdad), y ese método solo lee el repositorio: lo demás no se toca.
            var donaciones = new DonacionesService(_donaciones, null!, null!, null!, null!);

            // Igual con alquileres: el reporte solo usa el historial del servicio
            // real, que solo lee el repositorio.
            var alquileres = new AlquileresService(_alquileres, null!, null!, null!);

            // Y con inventario: el historial de movimientos solo lee el repositorio.
            var inventario = new InventarioService(_inventario, null!);

            _servicio = new ReportesService(_asistencias, new RepositorioBeneficiariosFalso(), _gastos, donaciones, _donaciones, _donantes,
                alquileres, _alquileres, inventario, _inventario, _proyectos);

            _alquiler = _tipos.Agregar("Alquiler de Equipo");
            _combustible = _tipos.Agregar("Combustible");
        }

        private GastoOperativo AgregarGasto(TipoGasto tipo, string proveedor, int dia, decimal monto, decimal iva,
            string formaPago = FormasPago.Contado, string moneda = TiposMoneda.Colones,
            EstadoGastoOperativo estado = EstadoGastoOperativo.Activo)
        {
            var gasto = DatosGasto.Entidad(_gastos.Gastos.Count + 1, tipo.Id, monto, iva, moneda, estado);
            gasto.Proveedor = proveedor;
            gasto.Fecha = new DateTime(2026, 9, dia);
            gasto.FormaPago = formaPago;
            _gastos.Gastos.Add(gasto);
            return gasto;
        }

        // Una instancia nueva de Beneficiario por asistencia, igual que hace EF con
        // AsNoTracking: dos asistencias de la misma persona traen dos objetos
        // distintos con el mismo Id. Con una sola asistencia por persona (los datos
        // de las primeras pruebas) el error de agrupar por objeto no se notaba.
        private void AgregarAsistencia(int beneficiarioId, string primerNombre, string tiempoComida, int dia)
        {
            _asistencias.Asistencias.Add(new AsistenciaComedor
            {
                Id = _asistencias.Asistencias.Count + 1,
                BeneficiarioId = beneficiarioId,
                Beneficiario = new Beneficiario
                {
                    Id = beneficiarioId,
                    PrimerNombre = primerNombre,
                    PrimerApellido = "Prueba",
                    FechaNacimiento = new DateTime(1990, 1, 1)
                },
                Fecha = new DateTime(2026, 9, dia),
                TiempoComida = tiempoComida
            });
        }

        private void AgregarDonacionDinero(string donante, decimal monto, string moneda, DateTime fecha, string? observaciones = null)
        {
            _donaciones.Dinero.Add(new DonacionDinero
            {
                Id = _donaciones.Dinero.Count + 1,
                Donante = new Donante { Nombre = donante },
                Monto = monto,
                Moneda = moneda,
                Fecha = fecha,
                Observaciones = observaciones
            });
        }

        private void AgregarDonacionEspecie(string donante, DateTime fecha, string articulo, int cantidad)
        {
            _donaciones.Especie.Add(new DonacionEspecie
            {
                Id = _donaciones.Especie.Count + 1,
                Donante = new Donante { Nombre = donante },
                Fecha = fecha,
                Detalles =
                {
                    new DetalleDonacionEspecie { NombreArticulo = articulo, Cantidad = cantidad, UnidadMedida = "kg" }
                }
            });
        }

        private static FiltrosReporteDonacionesDto TodasLasDonaciones() => new();

        private static int IdDeArticulo(string nombre) => nombre switch
        {
            "Arroz" => 1,
            "Frijoles" => 2,
            _ => 3
        };

        private void AgregarEntrada(string articulo, int cantidad, DateTime fecha, string origen = "Compra")
        {
            _inventario.Entradas.Add(new EntradaInventario
            {
                Id = _inventario.Entradas.Count + 1,
                ArticuloId = IdDeArticulo(articulo),
                Articulo = new Articulo { Id = IdDeArticulo(articulo), Nombre = articulo },
                Cantidad = cantidad,
                Fecha = fecha,
                Origen = origen
            });
        }

        private void AgregarSalida(string articulo, int cantidad, DateTime fecha, string tipo, string? destino = null)
        {
            _inventario.Salidas.Add(new SalidaInventario
            {
                Id = _inventario.Salidas.Count + 1,
                ArticuloId = IdDeArticulo(articulo),
                Articulo = new Articulo { Id = IdDeArticulo(articulo), Nombre = articulo },
                Cantidad = cantidad,
                Fecha = fecha,
                TipoSalida = tipo,
                ComunidadDestinataria = destino
            });
        }

        private static FiltrosReporteMovimientosDto TodosLosMovimientos() => new();

        private ProyectoComunitario AgregarProyecto(string nombre, DateTime inicio, EstadoProyecto estado = EstadoProyecto.EnCurso,
            int beneficiarios = 0, int externos = 0, DateTime? fechaFinalizacion = null, DateTime? registroParticipantes = null)
        {
            var proyecto = new ProyectoComunitario
            {
                Id = _proyectos.Proyectos.Count + 1,
                Nombre = nombre,
                FechaInicio = inicio,
                FechaEstimadaFin = inicio.AddMonths(3),
                FechaFinalizacionReal = fechaFinalizacion,
                Estado = estado
            };

            var registro = registroParticipantes ?? inicio;

            for (var i = 0; i < beneficiarios; i++)
                proyecto.Participantes.Add(new ParticipanteProyecto { EsBeneficiario = true, BeneficiarioId = i + 1, FechaRegistro = registro });

            for (var i = 0; i < externos; i++)
                proyecto.Participantes.Add(new ParticipanteProyecto { EsBeneficiario = false, NombreExterno = $"Externo {i + 1}", FechaRegistro = registro });

            _proyectos.Proyectos.Add(proyecto);
            return proyecto;
        }

        private static FiltrosReporteProyectosDto TodosLosProyectos() => new();

        [Fact]
        public async Task El_reporte_de_proyectos_arma_cada_fila_con_sus_fechas_y_participantes()
        {
            AgregarProyecto("Huerta comunitaria", new DateTime(2026, 3, 1), EstadoProyecto.Finalizado,
                beneficiarios: 5, externos: 2, fechaFinalizacion: new DateTime(2026, 6, 15));

            var resultado = await _servicio.GenerarReporteProyectosAsync(TodosLosProyectos());

            var fila = Assert.Single(resultado.Filas);
            Assert.Equal("Huerta comunitaria", fila.Nombre);
            Assert.Equal("Finalizado", fila.Estado);
            Assert.Equal(new DateTime(2026, 3, 1), fila.FechaInicio);
            Assert.Equal(new DateTime(2026, 6, 1), fila.FechaEstimadaFin);
            Assert.Equal(new DateTime(2026, 6, 15), fila.FechaFinalizacion);
            Assert.Equal(7, fila.TotalParticipantes);
            Assert.Equal(5, fila.Beneficiarios);
            Assert.Equal(2, fila.Externos);
        }

        [Fact]
        public async Task El_reporte_de_proyectos_escribe_En_curso_con_espacio_y_deja_vacia_la_finalizacion_sin_finalizar()
        {
            AgregarProyecto("Taller de costura", new DateTime(2026, 8, 1), EstadoProyecto.EnCurso);

            var resultado = await _servicio.GenerarReporteProyectosAsync(TodosLosProyectos());

            var fila = Assert.Single(resultado.Filas);
            Assert.Equal("En curso", fila.Estado);
            Assert.Null(fila.FechaFinalizacion);
        }

        [Fact]
        public async Task El_reporte_de_proyectos_cuenta_los_proyectos_de_cada_estado_y_suma_participantes()
        {
            AgregarProyecto("A", new DateTime(2026, 1, 1), EstadoProyecto.Planificado, beneficiarios: 1);
            AgregarProyecto("B", new DateTime(2026, 2, 1), EstadoProyecto.EnCurso, beneficiarios: 3, externos: 1);
            AgregarProyecto("C", new DateTime(2026, 3, 1), EstadoProyecto.EnCurso, externos: 2);
            AgregarProyecto("D", new DateTime(2026, 4, 1), EstadoProyecto.Finalizado, beneficiarios: 4);
            AgregarProyecto("E", new DateTime(2026, 5, 1), EstadoProyecto.Cancelado);

            var resultado = await _servicio.GenerarReporteProyectosAsync(TodosLosProyectos());

            Assert.Equal(5, resultado.Filas.Count);
            Assert.Equal(1, resultado.Planificados);
            Assert.Equal(2, resultado.EnCurso);
            Assert.Equal(1, resultado.Finalizados);
            Assert.Equal(1, resultado.Cancelados);
            Assert.Equal(11, resultado.TotalParticipantes);
            Assert.Equal(8, resultado.TotalBeneficiarios);
            Assert.Equal(3, resultado.TotalExternos);
        }

        [Fact]
        public async Task El_reporte_de_proyectos_filtra_por_estado()
        {
            AgregarProyecto("En curso", new DateTime(2026, 1, 1), EstadoProyecto.EnCurso);
            AgregarProyecto("Finalizado", new DateTime(2026, 2, 1), EstadoProyecto.Finalizado);

            var resultado = await _servicio.GenerarReporteProyectosAsync(new FiltrosReporteProyectosDto { Estado = EstadoProyecto.Finalizado });

            Assert.Equal("Finalizado", Assert.Single(resultado.Filas).Nombre);
            Assert.Equal(1, resultado.Finalizados);
            Assert.Equal(0, resultado.EnCurso);
        }

        [Fact]
        public async Task El_reporte_de_proyectos_filtra_por_la_fecha_de_inicio_incluyendo_el_ultimo_dia()
        {
            AgregarProyecto("Antes", new DateTime(2026, 8, 31, 23, 0, 0));
            AgregarProyecto("Primer día", new DateTime(2026, 9, 1, 8, 0, 0));
            AgregarProyecto("Último día", new DateTime(2026, 9, 30, 14, 30, 0));
            AgregarProyecto("Después", new DateTime(2026, 10, 1));

            var resultado = await _servicio.GenerarReporteProyectosAsync(new FiltrosReporteProyectosDto
            {
                FechaDesde = new DateTime(2026, 9, 1),
                FechaHasta = new DateTime(2026, 9, 30)
            });

            // Del más reciente al más antiguo, como el listado de proyectos.
            Assert.Equal(new[] { "Último día", "Primer día" }, resultado.Filas.Select(f => f.Nombre));
        }

        [Fact]
        public async Task El_reporte_de_proyectos_sin_resultados_viene_vacio_y_envuelve_los_errores()
        {
            var vacio = await _servicio.GenerarReporteProyectosAsync(TodosLosProyectos());

            Assert.Empty(vacio.Filas);
            Assert.Equal(0, vacio.TotalParticipantes);

            _proyectos.Falla = true;

            var error = await Assert.ThrowsAsync<Exception>(() => _servicio.GenerarReporteProyectosAsync(TodosLosProyectos()));
            Assert.Equal("Error al generar el reporte de proyectos comunitarios.", error.Message);
        }

        [Fact]
        public async Task El_panorama_de_proyectos_cuenta_estados_y_tipos_de_participante_de_todos_los_proyectos()
        {
            // Uno iniciado hace más de 12 meses y todavía en curso: cuenta igual.
            AgregarProyecto("Antiguo", DateTime.Today.AddMonths(-20), EstadoProyecto.EnCurso, beneficiarios: 2, externos: 1);
            AgregarProyecto("Reciente", DateTime.Today, EstadoProyecto.Planificado, beneficiarios: 1);
            AgregarProyecto("Cerrado", DateTime.Today.AddMonths(-3), EstadoProyecto.Finalizado, externos: 2);

            var panorama = await _servicio.ObtenerPanoramaProyectosAsync();

            Assert.Equal(3, panorama.TotalProyectos);
            Assert.Equal(1, panorama.Planificados);
            Assert.Equal(1, panorama.EnCurso);
            Assert.Equal(1, panorama.Finalizados);
            Assert.Equal(0, panorama.Cancelados);
            Assert.Equal(3, panorama.ParticipantesBeneficiarios);
            Assert.Equal(3, panorama.ParticipantesExternos);
        }

        [Fact]
        public async Task El_panorama_de_proyectos_acota_las_series_mensuales_a_la_ventana_de_12_meses()
        {
            var hace3Meses = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 5).AddMonths(-3);

            AgregarProyecto("Antiguo", DateTime.Today.AddMonths(-20), beneficiarios: 4);
            AgregarProyecto("Dentro", hace3Meses, beneficiarios: 2, externos: 1);
            AgregarProyecto("Otro dentro", hace3Meses.AddDays(2), beneficiarios: 1);

            var panorama = await _servicio.ObtenerPanoramaProyectosAsync();

            // Los iniciados por mes dejan fuera al antiguo.
            var iniciados = Assert.Single(panorama.ProyectosIniciadosPorMes);
            Assert.Equal((hace3Meses.Year, hace3Meses.Month, 2), (iniciados.Anio, iniciados.Mes, iniciados.Cantidad));

            // Los participantes por mes, también: solo los registrados en la ventana (4 + 0 quedan fuera).
            var registrados = Assert.Single(panorama.ParticipantesRegistradosPorMes);
            Assert.Equal(4, registrados.Cantidad);
        }

        [Fact]
        public async Task El_panorama_de_proyectos_ordena_los_que_tienen_mas_participantes_y_omite_los_vacios()
        {
            AgregarProyecto("Pocos", new DateTime(2026, 1, 1), beneficiarios: 1);
            AgregarProyecto("Muchos", new DateTime(2026, 2, 1), beneficiarios: 5, externos: 3);
            AgregarProyecto("Sin nadie", new DateTime(2026, 3, 1));
            AgregarProyecto("Medios", new DateTime(2026, 4, 1), beneficiarios: 4);
            AgregarProyecto("Empate A", new DateTime(2026, 5, 1), beneficiarios: 4);

            var panorama = await _servicio.ObtenerPanoramaProyectosAsync();

            // De mayor a menor y, en empate, por nombre; "Sin nadie" no aparece.
            Assert.Equal(new[] { ("Muchos", 8), ("Empate A", 4), ("Medios", 4), ("Pocos", 1) },
                panorama.ProyectosConMasParticipantes.Select(p => (p.Proyecto, p.Participantes)));
        }

        [Fact]
        public async Task El_panorama_de_proyectos_sin_datos_viene_en_cero_y_envuelve_los_errores()
        {
            var vacio = await _servicio.ObtenerPanoramaProyectosAsync();

            Assert.Equal(0, vacio.TotalProyectos);
            Assert.Empty(vacio.ProyectosConMasParticipantes);

            _proyectos.Falla = true;

            var error = await Assert.ThrowsAsync<Exception>(() => _servicio.ObtenerPanoramaProyectosAsync());
            Assert.Equal("Error al generar el panorama de proyectos comunitarios.", error.Message);
        }

        [Fact]
        public async Task El_panorama_de_inventario_suma_las_unidades_de_cada_lado_y_cada_tipo()
        {
            _inventario.EntradasPorMesYOrigen.Add(new UnidadesPorMesYTipoDto(2026, 8, "Compra", 50));
            _inventario.EntradasPorMesYOrigen.Add(new UnidadesPorMesYTipoDto(2026, 8, "Donacion", 30));
            _inventario.EntradasPorMesYOrigen.Add(new UnidadesPorMesYTipoDto(2026, 9, "Compra", 20));
            _inventario.SalidasPorMesYTipo.Add(new UnidadesPorMesYTipoDto(2026, 8, "Donacion", 15));
            _inventario.SalidasPorMesYTipo.Add(new UnidadesPorMesYTipoDto(2026, 9, "Donacion", 5));
            _inventario.SalidasPorMesYTipo.Add(new UnidadesPorMesYTipoDto(2026, 9, "Prestamo", 2));

            var panorama = await _servicio.ObtenerPanoramaInventarioAsync();

            Assert.Equal(100, panorama.UnidadesEntradas);
            Assert.Equal(22, panorama.UnidadesSalidas);
            Assert.Equal(70, panorama.EntradasPorCompra);
            Assert.Equal(30, panorama.EntradasPorDonacion);
            Assert.Equal(20, panorama.SalidasPorDonacion);
            Assert.Equal(2, panorama.SalidasPorPrestamo);
        }

        [Fact]
        public async Task El_panorama_de_inventario_colapsa_el_tipo_en_el_total_de_cada_mes()
        {
            _inventario.EntradasPorMesYOrigen.Add(new UnidadesPorMesYTipoDto(2026, 8, "Compra", 50));
            _inventario.EntradasPorMesYOrigen.Add(new UnidadesPorMesYTipoDto(2026, 8, "Donacion", 30));
            _inventario.EntradasPorMesYOrigen.Add(new UnidadesPorMesYTipoDto(2026, 9, "Compra", 20));
            _inventario.SalidasPorMesYTipo.Add(new UnidadesPorMesYTipoDto(2026, 9, "Donacion", 5));
            _inventario.SalidasPorMesYTipo.Add(new UnidadesPorMesYTipoDto(2026, 9, "Prestamo", 2));

            var panorama = await _servicio.ObtenerPanoramaInventarioAsync();

            Assert.Equal(new[] { (2026, 8, 80), (2026, 9, 20) }, panorama.EntradasPorMes.Select(m => (m.Anio, m.Mes, m.Cantidad)));
            Assert.Equal((2026, 9, 7), (panorama.SalidasPorMes.Single().Anio, panorama.SalidasPorMes.Single().Mes, panorama.SalidasPorMes.Single().Cantidad));
        }

        [Fact]
        public async Task El_panorama_de_inventario_reparte_los_articulos_y_pide_doce_meses_y_cinco_articulos()
        {
            _inventario.ArticulosConMasMovimientos.Add(new MovimientosPorArticuloDto("Arroz", 9));
            _inventario.ArticulosConMasMovimientos.Add(new MovimientosPorArticuloDto("Azucar", 4));

            var panorama = await _servicio.ObtenerPanoramaInventarioAsync();

            Assert.Equal(new[] { "Arroz", "Azucar" }, panorama.ArticulosConMasMovimientos.Select(a => a.Articulo));
            Assert.Equal(2, _inventario.Peticiones.Count(p => p == (12, null)));
            Assert.Contains((12, (int?)5), _inventario.Peticiones);
        }

        [Fact]
        public async Task El_panorama_de_inventario_sin_datos_viene_en_cero()
        {
            var panorama = await _servicio.ObtenerPanoramaInventarioAsync();

            Assert.Equal(0, panorama.UnidadesEntradas);
            Assert.Equal(0, panorama.UnidadesSalidas);
            Assert.Empty(panorama.EntradasPorMes);
            Assert.Empty(panorama.ArticulosConMasMovimientos);
        }

        [Fact]
        public async Task El_panorama_de_inventario_envuelve_los_errores_de_la_consulta()
        {
            _inventario.Falla = true;

            var error = await Assert.ThrowsAsync<Exception>(() => _servicio.ObtenerPanoramaInventarioAsync());

            Assert.Equal("Error al generar el panorama de inventario.", error.Message);
        }

        [Fact]
        public async Task El_reporte_de_movimientos_une_entradas_y_salidas_con_los_tipos_bien_escritos()
        {
            AgregarEntrada("Arroz", 50, new DateTime(2026, 9, 1), "Compra");
            AgregarEntrada("Frijoles", 20, new DateTime(2026, 9, 3), "Donacion");
            AgregarSalida("Arroz", 10, new DateTime(2026, 9, 5), TiposSalidaInventario.Donacion, "Comunidad El Roble");
            AgregarSalida("Frijoles", 2, new DateTime(2026, 9, 7), TiposSalidaInventario.Prestamo);

            var resultado = await _servicio.GenerarReporteMovimientosAsync(TodosLosMovimientos());

            // De la más reciente a la más antigua.
            Assert.Equal(new[] { "Préstamo", "Donación", "Entrada", "Entrada" }, resultado.Filas.Select(f => f.TipoMovimiento));

            var prestamo = resultado.Filas[0];
            Assert.Equal(new DateTime(2026, 9, 7), prestamo.Fecha);
            Assert.Equal("Frijoles", prestamo.Articulo);
            Assert.Equal(2, prestamo.Cantidad);
            // Sin destinatario, el destino es el tipo de salida, bien escrito.
            Assert.Equal("Préstamo", prestamo.OrigenODestino);

            Assert.Equal("Comunidad El Roble", resultado.Filas[1].OrigenODestino);
            Assert.Equal("Donación", resultado.Filas[2].OrigenODestino);
            Assert.Equal("Compra", resultado.Filas[3].OrigenODestino);
        }

        [Fact]
        public async Task El_reporte_de_movimientos_suma_unidades_y_no_cantidad_de_movimientos()
        {
            AgregarEntrada("Arroz", 50, new DateTime(2026, 9, 1));
            AgregarEntrada("Arroz", 30, new DateTime(2026, 9, 2));
            AgregarSalida("Arroz", 15, new DateTime(2026, 9, 5), TiposSalidaInventario.Donacion, "Comunidad El Roble");
            AgregarSalida("Frijoles", 5, new DateTime(2026, 9, 6), TiposSalidaInventario.Prestamo);

            var resultado = await _servicio.GenerarReporteMovimientosAsync(TodosLosMovimientos());

            Assert.Equal(4, resultado.Filas.Count);
            Assert.Equal(80, resultado.TotalEntradas);
            Assert.Equal(20, resultado.TotalSalidas);
        }

        [Theory]
        [InlineData("Entrada", 2, 0)]
        [InlineData("Donacion", 0, 1)]
        [InlineData("Prestamo", 0, 1)]
        [InlineData(null, 2, 2)]
        [InlineData("", 2, 2)]
        public async Task El_reporte_de_movimientos_filtra_por_tipo(string? tipo, int entradasEsperadas, int salidasEsperadas)
        {
            AgregarEntrada("Arroz", 50, new DateTime(2026, 9, 1));
            AgregarEntrada("Frijoles", 20, new DateTime(2026, 9, 2), "Donacion");
            AgregarSalida("Arroz", 10, new DateTime(2026, 9, 5), TiposSalidaInventario.Donacion, "Comunidad El Roble");
            AgregarSalida("Frijoles", 2, new DateTime(2026, 9, 7), TiposSalidaInventario.Prestamo);

            var resultado = await _servicio.GenerarReporteMovimientosAsync(new FiltrosReporteMovimientosDto { TipoMovimiento = tipo });

            Assert.Equal(entradasEsperadas, resultado.Filas.Count(f => f.TipoMovimiento == "Entrada"));
            Assert.Equal(salidasEsperadas, resultado.Filas.Count(f => f.TipoMovimiento != "Entrada"));
        }

        [Fact]
        public async Task El_reporte_de_movimientos_filtra_por_articulo()
        {
            AgregarEntrada("Arroz", 50, new DateTime(2026, 9, 1));
            AgregarEntrada("Frijoles", 20, new DateTime(2026, 9, 2));
            AgregarSalida("Frijoles", 2, new DateTime(2026, 9, 7), TiposSalidaInventario.Prestamo);

            var resultado = await _servicio.GenerarReporteMovimientosAsync(
                new FiltrosReporteMovimientosDto { ArticuloId = IdDeArticulo("Frijoles") });

            Assert.All(resultado.Filas, f => Assert.Equal("Frijoles", f.Articulo));
            Assert.Equal(2, resultado.Filas.Count);
            Assert.Equal(20, resultado.TotalEntradas);
            Assert.Equal(2, resultado.TotalSalidas);
        }

        [Fact]
        public async Task El_reporte_de_movimientos_filtra_por_fechas_incluyendo_el_ultimo_dia()
        {
            AgregarEntrada("Arroz", 1, new DateTime(2026, 8, 31, 23, 0, 0));
            AgregarEntrada("Arroz", 2, new DateTime(2026, 9, 1, 8, 0, 0));
            AgregarEntrada("Arroz", 4, new DateTime(2026, 9, 30, 14, 30, 0));
            AgregarSalida("Arroz", 8, new DateTime(2026, 10, 1), TiposSalidaInventario.Donacion);

            var resultado = await _servicio.GenerarReporteMovimientosAsync(new FiltrosReporteMovimientosDto
            {
                FechaDesde = new DateTime(2026, 9, 1),
                FechaHasta = new DateTime(2026, 9, 30)
            });

            Assert.Equal(new[] { 4, 2 }, resultado.Filas.Select(f => f.Cantidad));
            Assert.Equal(6, resultado.TotalEntradas);
            Assert.Equal(0, resultado.TotalSalidas);
        }

        [Fact]
        public async Task El_reporte_de_movimientos_sin_resultados_viene_vacio()
        {
            var resultado = await _servicio.GenerarReporteMovimientosAsync(TodosLosMovimientos());

            Assert.Empty(resultado.Filas);
            Assert.Equal(0, resultado.TotalEntradas);
            Assert.Equal(0, resultado.TotalSalidas);
        }

        [Fact]
        public async Task El_reporte_de_movimientos_envuelve_los_errores_del_historial()
        {
            _inventario.Falla = true;

            var error = await Assert.ThrowsAsync<Exception>(() => _servicio.GenerarReporteMovimientosAsync(TodosLosMovimientos()));

            Assert.Equal("Error al generar el reporte de movimientos de inventario.", error.Message);
        }

        private void AgregarAlquiler(string arrendatario, DateTime fecha, TimeSpan inicio, TimeSpan fin, decimal monto,
            string moneda = TiposMoneda.Colones, EstadoAlquiler estado = EstadoAlquiler.Reservado, int personas = 20,
            params string[] sectores)
        {
            _alquileres.Alquileres.Add(new AlquilerEspacio
            {
                Id = _alquileres.Alquileres.Count + 1,
                Arrendatario = new Arrendatario { Nombre = arrendatario },
                Fecha = fecha,
                HoraInicio = inicio,
                HoraFin = fin,
                CantidadPersonas = personas,
                Monto = monto,
                Moneda = moneda,
                Estado = estado,
                // El Id del sector sale de su nombre, igual en todos los alquileres.
                Espacios = sectores.Select(s => new EspacioFisico { Id = IdDeSector(s), Nombre = s }).ToList()
            });
        }

        private static int IdDeSector(string nombre) => nombre switch
        {
            "Salón principal" => 1,
            "Cocina" => 2,
            _ => 3
        };

        private static TimeSpan Hora(int horas, int minutos = 0) => new(horas, minutos, 0);

        private static FiltrosReporteAlquileresDto TodosLosAlquileres() => new();

        [Fact]
        public async Task El_reporte_de_alquileres_arma_horario_sectores_y_estado_como_texto()
        {
            AgregarAlquiler("Asociación Vecinal", new DateTime(2026, 9, 12), Hora(8), Hora(12), 50000m,
                sectores: new[] { "Salón principal", "Cocina" });

            var resultado = await _servicio.GenerarReporteAlquileresAsync(TodosLosAlquileres());

            var fila = Assert.Single(resultado.Filas);
            Assert.Equal(new DateTime(2026, 9, 12), fila.Fecha);
            Assert.Equal("8:00 a. m. – 12:00 p. m.", fila.Horario);
            Assert.Equal("Asociación Vecinal", fila.Arrendatario);
            // Los sectores salen ordenados por nombre, como en el calendario.
            Assert.Equal("Cocina, Salón principal", fila.Sectores);
            Assert.Equal(20, fila.Personas);
            Assert.Equal(50000m, fila.Monto);
            Assert.Equal(TiposMoneda.Colones, fila.Moneda);
            Assert.Equal("Reservado", fila.Estado);
        }

        [Fact]
        public async Task El_reporte_de_alquileres_cuenta_reservados_y_cancelados()
        {
            AgregarAlquiler("Ana", new DateTime(2026, 9, 1), Hora(8), Hora(10), 1000m, sectores: new[] { "Cocina" });
            AgregarAlquiler("Beto", new DateTime(2026, 9, 2), Hora(8), Hora(10), 1000m, sectores: new[] { "Cocina" });
            AgregarAlquiler("Carla", new DateTime(2026, 9, 3), Hora(8), Hora(10), 1000m,
                estado: EstadoAlquiler.Cancelado, sectores: new[] { "Cocina" });

            var resultado = await _servicio.GenerarReporteAlquileresAsync(TodosLosAlquileres());

            Assert.Equal(2, resultado.CantidadReservados);
            Assert.Equal(1, resultado.CantidadCancelados);
            Assert.Equal("Cancelado", resultado.Filas.Single(f => f.Arrendatario == "Carla").Estado);
        }

        [Fact]
        public async Task El_reporte_de_alquileres_suma_ingresos_por_moneda_sin_contar_cancelados()
        {
            AgregarAlquiler("Ana", new DateTime(2026, 9, 1), Hora(8), Hora(10), 40000m, sectores: new[] { "Cocina" });
            AgregarAlquiler("Beto", new DateTime(2026, 9, 2), Hora(8), Hora(10), 10000m, sectores: new[] { "Cocina" });
            AgregarAlquiler("Carla", new DateTime(2026, 9, 3), Hora(8), Hora(10), 100m, TiposMoneda.Dolares, sectores: new[] { "Cocina" });
            AgregarAlquiler("Dora", new DateTime(2026, 9, 4), Hora(8), Hora(10), 99999m,
                estado: EstadoAlquiler.Cancelado, sectores: new[] { "Cocina" });

            var resultado = await _servicio.GenerarReporteAlquileresAsync(TodosLosAlquileres());

            Assert.Equal(2, resultado.IngresosPorMoneda.Count);
            // En el orden del catálogo de monedas: colones antes que dólares.
            Assert.Equal(new[] { TiposMoneda.Colones, TiposMoneda.Dolares }, resultado.IngresosPorMoneda.Select(t => t.Moneda));
            Assert.Equal(50000m, resultado.IngresosPorMoneda[0].Total);
            Assert.Equal(100m, resultado.IngresosPorMoneda[1].Total);
        }

        [Fact]
        public async Task El_reporte_de_alquileres_calcula_las_horas_de_los_reservados_una_vez_por_alquiler()
        {
            // 4 h en dos sectores (cuenta 4, no 8) + 2 h 30 min en uno.
            AgregarAlquiler("Ana", new DateTime(2026, 9, 1), Hora(8), Hora(12), 1000m,
                sectores: new[] { "Salón principal", "Cocina" });
            AgregarAlquiler("Beto", new DateTime(2026, 9, 2), Hora(14), Hora(16, 30), 1000m, sectores: new[] { "Cocina" });
            // Cancelado: no ocupa el local, no suma horas.
            AgregarAlquiler("Carla", new DateTime(2026, 9, 3), Hora(8), Hora(18), 1000m,
                estado: EstadoAlquiler.Cancelado, sectores: new[] { "Cocina" });

            var resultado = await _servicio.GenerarReporteAlquileresAsync(TodosLosAlquileres());

            Assert.Equal(6.5m, resultado.HorasAlquiladas);
        }

        [Fact]
        public async Task El_reporte_de_alquileres_filtra_por_fechas_incluyendo_el_ultimo_dia()
        {
            AgregarAlquiler("Antes", new DateTime(2026, 8, 31), Hora(8), Hora(10), 1m, sectores: new[] { "Cocina" });
            AgregarAlquiler("Primer día", new DateTime(2026, 9, 1), Hora(8), Hora(10), 2m, sectores: new[] { "Cocina" });
            AgregarAlquiler("Último día", new DateTime(2026, 9, 30), Hora(8), Hora(10), 4m, sectores: new[] { "Cocina" });
            AgregarAlquiler("Después", new DateTime(2026, 10, 1), Hora(8), Hora(10), 8m, sectores: new[] { "Cocina" });

            var resultado = await _servicio.GenerarReporteAlquileresAsync(new FiltrosReporteAlquileresDto
            {
                FechaDesde = new DateTime(2026, 9, 1),
                FechaHasta = new DateTime(2026, 9, 30)
            });

            Assert.Equal(new[] { "Primer día", "Último día" }, resultado.Filas.Select(f => f.Arrendatario));
            Assert.Equal(6m, Assert.Single(resultado.IngresosPorMoneda).Total);
        }

        [Fact]
        public async Task El_reporte_de_alquileres_filtra_por_sector_y_por_estado()
        {
            AgregarAlquiler("Solo cocina", new DateTime(2026, 9, 1), Hora(8), Hora(10), 1m, sectores: new[] { "Cocina" });
            AgregarAlquiler("Ambos", new DateTime(2026, 9, 2), Hora(8), Hora(10), 1m, sectores: new[] { "Salón principal", "Cocina" });
            AgregarAlquiler("Solo salón", new DateTime(2026, 9, 3), Hora(8), Hora(10), 1m, sectores: new[] { "Salón principal" });
            AgregarAlquiler("Cocina cancelada", new DateTime(2026, 9, 4), Hora(8), Hora(10), 1m,
                estado: EstadoAlquiler.Cancelado, sectores: new[] { "Cocina" });

            var porSector = await _servicio.GenerarReporteAlquileresAsync(new FiltrosReporteAlquileresDto { EspacioId = IdDeSector("Cocina") });
            var porEstado = await _servicio.GenerarReporteAlquileresAsync(new FiltrosReporteAlquileresDto { Estado = EstadoAlquiler.Cancelado });

            // Los que usan la cocina, entre otros sectores.
            Assert.Equal(new[] { "Solo cocina", "Ambos", "Cocina cancelada" }, porSector.Filas.Select(f => f.Arrendatario));
            Assert.Equal("Cocina cancelada", Assert.Single(porEstado.Filas).Arrendatario);
            Assert.Equal(1, porEstado.CantidadCancelados);
            Assert.Equal(0, porEstado.CantidadReservados);
        }

        [Fact]
        public async Task El_reporte_de_alquileres_sin_resultados_viene_vacio()
        {
            var resultado = await _servicio.GenerarReporteAlquileresAsync(TodosLosAlquileres());

            Assert.Empty(resultado.Filas);
            Assert.Empty(resultado.IngresosPorMoneda);
            Assert.Equal(0, resultado.CantidadReservados);
            Assert.Equal(0, resultado.CantidadCancelados);
            Assert.Equal(0m, resultado.HorasAlquiladas);
        }

        private void AgregarAlquilerPanorama(DateTime fecha, TimeSpan inicio, TimeSpan fin, decimal monto,
            string moneda = TiposMoneda.Colones, EstadoAlquiler estado = EstadoAlquiler.Reservado, params string[] sectores)
        {
            _alquileres.Panorama.Add(new AlquilerPanoramaDto(fecha, inicio, fin, monto, moneda, estado, sectores));
        }

        [Fact]
        public async Task El_panorama_de_alquileres_cuenta_reservados_y_cancelados_por_mes()
        {
            AgregarAlquilerPanorama(new DateTime(2026, 8, 3), Hora(8), Hora(10), 1m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 7), Hora(8), Hora(10), 1m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 8), Hora(8), Hora(10), 1m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 9), Hora(8), Hora(10), 1m, estado: EstadoAlquiler.Cancelado, sectores: new[] { "Cocina" });

            var panorama = await _servicio.ObtenerPanoramaAlquileresAsync();

            Assert.Equal(3, panorama.CantidadReservados);
            Assert.Equal(1, panorama.CantidadCancelados);
            Assert.Equal(new[] { (2026, 8, 1), (2026, 9, 2) }, panorama.ReservadosPorMes.Select(c => (c.Anio, c.Mes, c.Cantidad)));
            Assert.Equal((2026, 9, 1), (panorama.CanceladosPorMes[0].Anio, panorama.CanceladosPorMes[0].Mes, panorama.CanceladosPorMes[0].Cantidad));
        }

        [Fact]
        public async Task El_panorama_de_alquileres_suma_ingresos_solo_en_colones_y_sin_cancelados()
        {
            AgregarAlquilerPanorama(new DateTime(2026, 8, 3), Hora(8), Hora(10), 30000m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 7), Hora(8), Hora(10), 20000m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 8), Hora(8), Hora(10), 15000m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 9), Hora(8), Hora(10), 999m, TiposMoneda.Dolares, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 10), Hora(8), Hora(10), 88888m, estado: EstadoAlquiler.Cancelado, sectores: new[] { "Cocina" });

            var panorama = await _servicio.ObtenerPanoramaAlquileresAsync();

            Assert.Equal(65000m, panorama.IngresosEnColones);
            Assert.Equal(new[] { (2026, 8, 30000m), (2026, 9, 35000m) },
                panorama.IngresosEnColonesPorMes.Select(m => (m.Anio, m.Mes, m.Monto)));
        }

        [Fact]
        public async Task El_panorama_de_alquileres_calcula_las_horas_de_los_reservados_por_mes()
        {
            AgregarAlquilerPanorama(new DateTime(2026, 8, 3), Hora(8), Hora(12), 1m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 7), Hora(14), Hora(16, 30), 1m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 8), Hora(9), Hora(10), 1m, sectores: new[] { "Cocina" });
            // Cancelado: no ocupa el local.
            AgregarAlquilerPanorama(new DateTime(2026, 9, 9), Hora(8), Hora(18), 1m, estado: EstadoAlquiler.Cancelado, sectores: new[] { "Cocina" });

            var panorama = await _servicio.ObtenerPanoramaAlquileresAsync();

            Assert.Equal(7.5m, panorama.HorasAlquiladas);
            Assert.Equal(new[] { (2026, 8, 4m), (2026, 9, 3.5m) }, panorama.HorasPorMes.Select(h => (h.Anio, h.Mes, h.Horas)));
        }

        [Fact]
        public async Task El_panorama_de_alquileres_cuenta_un_alquiler_de_dos_sectores_en_cada_uno()
        {
            AgregarAlquilerPanorama(new DateTime(2026, 9, 1), Hora(8), Hora(10), 1m, sectores: new[] { "Salón principal", "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 2), Hora(8), Hora(10), 1m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 3), Hora(8), Hora(10), 1m, sectores: new[] { "Baños" });
            // Cancelado: no cuenta para ningún sector.
            AgregarAlquilerPanorama(new DateTime(2026, 9, 4), Hora(8), Hora(10), 1m, estado: EstadoAlquiler.Cancelado, sectores: new[] { "Baños" });

            var panorama = await _servicio.ObtenerPanoramaAlquileresAsync();

            // De mayor a menor y, en empate, por nombre.
            Assert.Equal(new[] { ("Cocina", 2), ("Baños", 1), ("Salón principal", 1) },
                panorama.AlquileresPorSector.Select(s => (s.Sector, s.Cantidad)));
        }

        [Fact]
        public async Task El_panorama_de_alquileres_separa_entre_semana_y_fin_de_semana()
        {
            // 2026-09-12 es sábado, 13 domingo y 14 lunes.
            AgregarAlquilerPanorama(new DateTime(2026, 9, 12), Hora(8), Hora(10), 1m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 13), Hora(8), Hora(10), 1m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 14), Hora(8), Hora(10), 1m, sectores: new[] { "Cocina" });
            AgregarAlquilerPanorama(new DateTime(2026, 9, 19), Hora(8), Hora(10), 1m, estado: EstadoAlquiler.Cancelado, sectores: new[] { "Cocina" });

            var panorama = await _servicio.ObtenerPanoramaAlquileresAsync();

            Assert.Equal(2, panorama.ReservadosFinDeSemana);
            Assert.Equal(1, panorama.ReservadosEntreSemana);
        }

        [Fact]
        public async Task El_panorama_de_alquileres_pide_doce_meses_y_sin_datos_viene_en_cero()
        {
            var panorama = await _servicio.ObtenerPanoramaAlquileresAsync();

            Assert.Equal(new[] { 12 }, _alquileres.MesesPedidos);
            Assert.Equal(0, panorama.CantidadReservados);
            Assert.Equal(0, panorama.CantidadCancelados);
            Assert.Equal(0m, panorama.HorasAlquiladas);
            Assert.Empty(panorama.AlquileresPorSector);
            Assert.Empty(panorama.ReservadosPorMes);
        }

        [Fact]
        public async Task El_panorama_de_alquileres_envuelve_los_errores_de_la_consulta()
        {
            _alquileres.Falla = true;

            var error = await Assert.ThrowsAsync<Exception>(() => _servicio.ObtenerPanoramaAlquileresAsync());

            Assert.Equal("Error al generar el panorama de alquileres.", error.Message);
        }

        [Fact]
        public async Task El_reporte_de_alquileres_envuelve_los_errores_del_historial()
        {
            _alquileres.Falla = true;

            var error = await Assert.ThrowsAsync<Exception>(() => _servicio.GenerarReporteAlquileresAsync(TodosLosAlquileres()));

            Assert.Equal("Error al generar el reporte de alquileres.", error.Message);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_une_dinero_y_especie_de_la_mas_reciente_a_la_mas_antigua()
        {
            AgregarDonacionDinero("Ana Mora", 25000m, TiposMoneda.Colones, new DateTime(2026, 9, 3), "Para el comedor");
            AgregarDonacionEspecie("Super Valle", new DateTime(2026, 9, 10), "Arroz", 3);

            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Equal(2, resultado.Filas.Count);

            var especie = resultado.Filas[0];
            Assert.Equal(new DateTime(2026, 9, 10), especie.Fecha);
            Assert.Equal("Especie", especie.TipoDonacion);
            Assert.Equal("Super Valle", especie.Donante);
            Assert.Equal("3 kg de Arroz", especie.Descripcion);

            // La especie no se valoriza: sin monto ni moneda, no un 0.
            Assert.Null(especie.Monto);
            Assert.Null(especie.Moneda);

            var dinero = resultado.Filas[1];
            Assert.Equal("Dinero", dinero.TipoDonacion);
            Assert.Equal("Ana Mora", dinero.Donante);
            Assert.Equal(25000m, dinero.Monto);
            Assert.Equal(TiposMoneda.Colones, dinero.Moneda);
            Assert.Equal("Para el comedor", dinero.Descripcion);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_cuenta_las_de_cada_clase()
        {
            AgregarDonacionDinero("Ana Mora", 1000m, TiposMoneda.Colones, new DateTime(2026, 9, 1));
            AgregarDonacionDinero("Beto Solís", 2000m, TiposMoneda.Colones, new DateTime(2026, 9, 2));
            AgregarDonacionEspecie("Super Valle", new DateTime(2026, 9, 3), "Arroz", 3);

            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Equal(2, resultado.CantidadDinero);
            Assert.Equal(1, resultado.CantidadEspecie);
            Assert.Equal(3, resultado.Filas.Count);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_totaliza_el_dinero_por_moneda_sin_mezclarlas()
        {
            AgregarDonacionDinero("Ana Mora", 1000m, TiposMoneda.Colones, new DateTime(2026, 9, 1));
            AgregarDonacionDinero("Beto Solís", 500m, TiposMoneda.Colones, new DateTime(2026, 9, 2));
            AgregarDonacionDinero("Carla Rojas", 20m, TiposMoneda.Dolares, new DateTime(2026, 9, 3));

            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Equal(2, resultado.TotalesPorMoneda.Count);
            Assert.Equal(1500m, Assert.Single(resultado.TotalesPorMoneda, t => t.Moneda == TiposMoneda.Colones).Total);
            Assert.Equal(20m, Assert.Single(resultado.TotalesPorMoneda, t => t.Moneda == TiposMoneda.Dolares).Total);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_lista_los_totales_en_el_orden_del_catalogo_de_monedas()
        {
            // De la más reciente a la más antigua aparecen euros, dólares y colones:
            // el total tiene que salir al revés, igual que TiposMoneda.Todos.
            AgregarDonacionDinero("Ana Mora", 1000m, TiposMoneda.Colones, new DateTime(2026, 9, 1));
            AgregarDonacionDinero("Carla Rojas", 20m, TiposMoneda.Dolares, new DateTime(2026, 9, 2));
            AgregarDonacionDinero("Dora Vega", 15m, TiposMoneda.Euros, new DateTime(2026, 9, 3));

            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Equal(TiposMoneda.Todos, resultado.TotalesPorMoneda.Select(t => t.Moneda));
        }

        [Fact]
        public async Task Las_donaciones_en_especie_no_suman_dinero()
        {
            AgregarDonacionEspecie("Super Valle", new DateTime(2026, 9, 3), "Arroz", 3);
            AgregarDonacionEspecie("Super Valle", new DateTime(2026, 9, 4), "Frijoles", 5);

            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Equal(2, resultado.CantidadEspecie);
            Assert.Equal(0, resultado.CantidadDinero);
            Assert.Empty(resultado.TotalesPorMoneda);
        }

        [Theory]
        [InlineData("Dinero", 1, 0)]
        [InlineData("Especie", 0, 1)]
        [InlineData(null, 1, 1)]
        [InlineData("", 1, 1)]
        public async Task El_reporte_de_donaciones_filtra_por_tipo(string? tipo, int esperadasDinero, int esperadasEspecie)
        {
            AgregarDonacionDinero("Ana Mora", 1000m, TiposMoneda.Colones, new DateTime(2026, 9, 1));
            AgregarDonacionEspecie("Super Valle", new DateTime(2026, 9, 3), "Arroz", 3);

            var resultado = await _servicio.GenerarReporteDonacionesAsync(new FiltrosReporteDonacionesDto { TipoDonacion = tipo });

            Assert.Equal(esperadasDinero, resultado.CantidadDinero);
            Assert.Equal(esperadasEspecie, resultado.CantidadEspecie);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_filtra_por_fechas_e_incluye_el_ultimo_dia()
        {
            AgregarDonacionDinero("Antes", 1m, TiposMoneda.Colones, new DateTime(2026, 8, 31, 23, 0, 0));
            AgregarDonacionDinero("Primer día", 2m, TiposMoneda.Colones, new DateTime(2026, 9, 1, 8, 0, 0));
            AgregarDonacionDinero("Último día", 4m, TiposMoneda.Colones, new DateTime(2026, 9, 30, 14, 30, 0));
            AgregarDonacionDinero("Después", 8m, TiposMoneda.Colones, new DateTime(2026, 10, 1, 0, 0, 0));

            var resultado = await _servicio.GenerarReporteDonacionesAsync(new FiltrosReporteDonacionesDto
            {
                FechaDesde = new DateTime(2026, 9, 1),
                FechaHasta = new DateTime(2026, 9, 30)
            });

            Assert.Equal(new[] { "Último día", "Primer día" }, resultado.Filas.Select(f => f.Donante));
            Assert.Equal(6m, Assert.Single(resultado.TotalesPorMoneda).Total);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_sin_resultados_viene_vacio()
        {
            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Empty(resultado.Filas);
            Assert.Empty(resultado.TotalesPorMoneda);
            Assert.Equal(0, resultado.CantidadDinero);
            Assert.Equal(0, resultado.CantidadEspecie);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_envuelve_los_errores_del_historial()
        {
            _donaciones.Falla = true;

            var error = await Assert.ThrowsAsync<Exception>(() => _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones()));

            Assert.Equal("Error al generar el reporte de donaciones.", error.Message);
        }

        [Fact]
        public async Task El_panorama_de_donaciones_suma_las_cantidades_de_cada_clase()
        {
            _donaciones.CantidadPorMes.Add(new ConteoDonacionMensualDto(2026, 8, "Dinero", 3));
            _donaciones.CantidadPorMes.Add(new ConteoDonacionMensualDto(2026, 8, "Especie", 1));
            _donaciones.CantidadPorMes.Add(new ConteoDonacionMensualDto(2026, 9, "Dinero", 4));
            _donaciones.CantidadPorMes.Add(new ConteoDonacionMensualDto(2026, 9, "Especie", 2));

            var panorama = await _servicio.ObtenerPanoramaDonacionesAsync();

            Assert.Equal(7, panorama.CantidadDinero);
            Assert.Equal(3, panorama.CantidadEspecie);
            Assert.Equal(4, panorama.DonacionesPorMes.Count);
        }

        [Fact]
        public async Task El_panorama_de_donaciones_reparte_el_dinero_los_donantes_y_su_estado()
        {
            _donaciones.DineroEnColonesPorMes.Add(new MontoPorMesDto(2026, 9, 175000.50m));
            _donaciones.TopDonantes.Add(new DonacionesPorDonanteDto("Ana Mora", 5));
            _donaciones.TopDonantes.Add(new DonacionesPorDonanteDto("Beto Solís", 2));
            _donantes.Resumen = new SIGAC.Application.DTOs.ResumenRegistrosDto(Activos: 8, Inactivos: 3, NuevosEsteMes: 0, NuevosMesAnterior: 0);

            var panorama = await _servicio.ObtenerPanoramaDonacionesAsync();

            Assert.Equal(175000.50m, Assert.Single(panorama.DineroEnColonesPorMes).Monto);
            Assert.Equal(new[] { "Ana Mora", "Beto Solís" }, panorama.TopDonantes.Select(d => d.Donante));
            Assert.Equal(8, panorama.DonantesActivos);
            Assert.Equal(3, panorama.DonantesInactivos);
        }

        [Fact]
        public async Task El_panorama_de_donaciones_pide_doce_meses_y_los_cinco_donantes_principales()
        {
            await _servicio.ObtenerPanoramaDonacionesAsync();

            Assert.Contains((12, (int?)null), _donaciones.Peticiones);
            Assert.Contains((12, (int?)5), _donaciones.Peticiones);
        }

        [Fact]
        public async Task El_panorama_de_donaciones_sin_datos_viene_en_cero()
        {
            var panorama = await _servicio.ObtenerPanoramaDonacionesAsync();

            Assert.Equal(0, panorama.CantidadDinero);
            Assert.Equal(0, panorama.CantidadEspecie);
            Assert.Empty(panorama.DonacionesPorMes);
            Assert.Empty(panorama.TopDonantes);
        }

        [Fact]
        public async Task El_panorama_de_donaciones_envuelve_los_errores_de_la_consulta()
        {
            _donaciones.Falla = true;

            var error = await Assert.ThrowsAsync<Exception>(() => _servicio.ObtenerPanoramaDonacionesAsync());

            Assert.Equal("Error al generar el panorama de donaciones.", error.Message);
        }

        [Fact]
        public async Task El_reporte_de_beneficiarios_da_una_fila_por_persona_aunque_asista_varias_veces()
        {
            AgregarAsistencia(1, "Ana", TiemposComida.Desayuno, 1);
            AgregarAsistencia(1, "Ana", TiemposComida.Almuerzo, 1);
            AgregarAsistencia(1, "Ana", TiemposComida.Merienda, 2);
            AgregarAsistencia(2, "Beto", TiemposComida.Desayuno, 1);

            var resultado = await _servicio.GenerarReporteBeneficiariosAsync(new FiltrosReporteBeneficiariosDto());

            Assert.Equal(2, resultado.Filas.Count);

            var ana = Assert.Single(resultado.Filas, f => f.BeneficiarioId == 1);
            Assert.Equal(1, ana.Desayunos);
            Assert.Equal(1, ana.Almuerzos);
            Assert.Equal(1, ana.Meriendas);
            Assert.Equal(3, ana.TotalAsistencias);

            Assert.Equal(1, Assert.Single(resultado.Filas, f => f.BeneficiarioId == 2).TotalAsistencias);
            Assert.Equal(4, resultado.TotalGeneral);
        }

        [Fact]
        public async Task El_reporte_de_beneficiarios_ordena_las_filas_por_nombre()
        {
            AgregarAsistencia(2, "Beto", TiemposComida.Desayuno, 1);
            AgregarAsistencia(1, "Ana", TiemposComida.Desayuno, 1);

            var resultado = await _servicio.GenerarReporteBeneficiariosAsync(new FiltrosReporteBeneficiariosDto());

            Assert.Equal(new[] { 1, 2 }, resultado.Filas.Select(f => f.BeneficiarioId));
        }

        [Fact]
        public async Task Agrupa_por_tipo_de_gasto_y_descripcion_de_cuenta()
        {
            AgregarGasto(_alquiler, "Renta Equipos Salas S.A", 5, 25000m, 3250m);
            AgregarGasto(_alquiler, "Renta Equipos Salas S.A", 17, 29370m, 3818m);
            AgregarGasto(_combustible, "Transgas Liberia S.A", 10, 32000m, 0m);

            var filtros = new FiltrosReporteGastosDto { Mes = 9, Anio = 2026, FormaPago = FormasPago.Contado };
            var reporte = await _servicio.GenerarReporteGastosAsync(filtros);

            Assert.Equal(2, reporte.Grupos.Count);

            var grupoAlquiler = Assert.Single(reporte.Grupos, g => g.TipoGasto == "Alquiler de Equipo");
            Assert.Equal(2, grupoAlquiler.Filas.Count);
            Assert.Equal(54370m, grupoAlquiler.SubtotalMonto);
            Assert.Equal(7068m, grupoAlquiler.SubtotalIva);

            var grupoCombustible = Assert.Single(reporte.Grupos, g => g.TipoGasto == "Combustible");
            Assert.Equal(32000m, grupoCombustible.SubtotalMonto);
            Assert.Equal(0m, grupoCombustible.SubtotalIva);
        }

        [Fact]
        public async Task Ordena_las_filas_de_cada_grupo_por_dia()
        {
            AgregarGasto(_alquiler, "Proveedor B", 27, 1000m, 0m);
            AgregarGasto(_alquiler, "Proveedor A", 1, 2000m, 0m);
            AgregarGasto(_alquiler, "Proveedor C", 11, 3000m, 0m);

            var filtros = new FiltrosReporteGastosDto { Mes = 9, Anio = 2026, FormaPago = FormasPago.Contado };
            var reporte = await _servicio.GenerarReporteGastosAsync(filtros);

            var grupo = Assert.Single(reporte.Grupos);
            Assert.Equal(new[] { 1, 11, 27 }, grupo.Filas.Select(f => f.Dia));
        }

        [Fact]
        public async Task Calcula_el_gran_total_sumando_los_subtotales_de_cada_grupo()
        {
            AgregarGasto(_alquiler, "Renta Equipos Salas S.A", 5, 25000m, 3250m);
            AgregarGasto(_combustible, "Transgas Liberia S.A", 10, 32000m, 1000m);

            var filtros = new FiltrosReporteGastosDto { Mes = 9, Anio = 2026, FormaPago = FormasPago.Contado };
            var reporte = await _servicio.GenerarReporteGastosAsync(filtros);

            Assert.Equal(57000m, reporte.GranTotalMonto);
            Assert.Equal(4250m, reporte.GranTotalIva);
        }

        [Fact]
        public async Task Excluye_los_gastos_anulados()
        {
            AgregarGasto(_alquiler, "Renta Equipos Salas S.A", 5, 25000m, 3250m);
            AgregarGasto(_alquiler, "Renta Equipos Salas S.A", 10, 99999m, 0m, estado: EstadoGastoOperativo.Anulado);

            var filtros = new FiltrosReporteGastosDto { Mes = 9, Anio = 2026, FormaPago = FormasPago.Contado };
            var reporte = await _servicio.GenerarReporteGastosAsync(filtros);

            var grupo = Assert.Single(reporte.Grupos);
            Assert.Equal(25000m, Assert.Single(grupo.Filas).Monto);
        }

        [Fact]
        public async Task Excluye_gastos_de_otro_mes_otra_forma_de_pago_u_otra_moneda()
        {
            AgregarGasto(_alquiler, "Dentro del filtro", 5, 25000m, 0m);
            AgregarGasto(_alquiler, "Otro mes", 5, 1m, 0m).Fecha = new DateTime(2026, 8, 5);
            AgregarGasto(_alquiler, "Otra forma de pago", 5, 1m, 0m, formaPago: FormasPago.Credito);
            AgregarGasto(_alquiler, "Otra moneda", 5, 1m, 0m, moneda: TiposMoneda.Dolares);

            var filtros = new FiltrosReporteGastosDto { Mes = 9, Anio = 2026, FormaPago = FormasPago.Contado };
            var reporte = await _servicio.GenerarReporteGastosAsync(filtros);

            var grupo = Assert.Single(reporte.Grupos);
            var fila = Assert.Single(grupo.Filas);
            Assert.Equal("Dentro del filtro", fila.Proveedor);
        }

        // Fakes mínimos: ReportesService los exige en el constructor, pero el
        // reporte de gastos no los usa. Si algún método de acá llega a
        // invocarse, que la prueba falle fuerte en vez de devolver datos falsos.
        private sealed class RepositorioAsistenciaFalso : IAsistenciaRepository
        {
            public Task AgregarAsync(AsistenciaComedor asistencia) => throw new NotImplementedException();
            public Task<bool> ExisteAsistenciaAsync(int beneficiarioId, DateTime fecha, string tiempoComida) => throw new NotImplementedException();
            public Task<IEnumerable<AsistenciaComedor>> ObtenerAsistenciasDiariasAsync(DateTime fecha) => throw new NotImplementedException();
            public Task<IReadOnlyList<AsistenciaComedor>> ObtenerPaginaHistorialAsync(SIGAC.Application.DTOs.Asistencia.FiltrosAsistenciaDto filtros) => throw new NotImplementedException();
            public Task<IReadOnlyDictionary<string, int>> ObtenerTotalesPorTiempoComidaAsync(SIGAC.Application.DTOs.Asistencia.FiltrosAsistenciaDto filtros) => throw new NotImplementedException();
            public List<AsistenciaComedor> Asistencias { get; } = new();

            public Task<IEnumerable<AsistenciaComedor>> ObtenerParaReporteBeneficiariosAsync(FiltrosReporteBeneficiariosDto filtros) =>
                Task.FromResult<IEnumerable<AsistenciaComedor>>(Asistencias);
            public Task<IReadOnlyList<ConteoComidaMensualDto>> ObtenerComidasPorTiempoYMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoPorMesDto>> ObtenerPersonasAtendidasPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
        }

        // Solo las dos consultas del historial, con el mismo criterio de fechas que
        // DonacionesRepositoryEfCore (el último día entra completo). El resto no lo
        // usa el reporte.
        private sealed class RepositorioDonacionesFalso : IDonacionesRepository
        {
            public List<DonacionDinero> Dinero { get; } = new();
            public List<DonacionEspecie> Especie { get; } = new();
            public bool Falla { get; set; }

            public Task<IEnumerable<DonacionDinero>> ObtenerDonacionesDineroAsync(FiltrosHistorialDonacionDto filtros) =>
                Task.FromResult(Consultar(Dinero, d => d.Fecha, filtros));

            public Task<IEnumerable<DonacionEspecie>> ObtenerDonacionesEspecieAsync(FiltrosHistorialDonacionDto filtros) =>
                Task.FromResult(Consultar(Especie, d => d.Fecha, filtros));

            private IEnumerable<T> Consultar<T>(List<T> donaciones, Func<T, DateTime> fecha, FiltrosHistorialDonacionDto filtros)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                return donaciones
                    .Where(d => filtros.FechaDesde is null || fecha(d) >= filtros.FechaDesde.Value.Date)
                    .Where(d => filtros.FechaHasta is null || fecha(d) < filtros.FechaHasta.Value.Date.AddDays(1))
                    .ToList();
            }

            // Lo que devuelven las consultas del panorama y con qué parámetros se
            // pidieron: el servicio solo las reparte, así que la prueba controla la
            // entrada.
            public List<ConteoDonacionMensualDto> CantidadPorMes { get; } = new();
            public List<MontoPorMesDto> DineroEnColonesPorMes { get; } = new();
            public List<DonacionesPorDonanteDto> TopDonantes { get; } = new();
            public List<(int Meses, int? Maximo)> Peticiones { get; } = new();

            public Task<IReadOnlyList<ConteoDonacionMensualDto>> ObtenerCantidadPorMesAsync(int mesesHaciaAtras) =>
                Panorama<ConteoDonacionMensualDto>(mesesHaciaAtras, null, CantidadPorMes);

            public Task<IReadOnlyList<MontoPorMesDto>> ObtenerDineroEnColonesPorMesAsync(int mesesHaciaAtras) =>
                Panorama<MontoPorMesDto>(mesesHaciaAtras, null, DineroEnColonesPorMes);

            public Task<IReadOnlyList<DonacionesPorDonanteDto>> ObtenerTopDonantesAsync(int mesesHaciaAtras, int maximo) =>
                Panorama<DonacionesPorDonanteDto>(mesesHaciaAtras, maximo, TopDonantes);

            private Task<IReadOnlyList<T>> Panorama<T>(int meses, int? maximo, List<T> datos)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                Peticiones.Add((meses, maximo));
                return Task.FromResult<IReadOnlyList<T>>(datos.ToList());
            }

            public Task AgregarDonacionDineroAsync(DonacionDinero donacion) => throw new NotImplementedException();
            public Task AgregarDonacionEspecieAsync(DonacionEspecie donacion) => throw new NotImplementedException();
            public Task AgregarDonacionEntregadaAsync(DonacionEntregada donacion) => throw new NotImplementedException();
            public Task<IEnumerable<DonacionEntregada>> ObtenerEntregasAsync(FiltrosHistorialEntregaDto filtros) => throw new NotImplementedException();

            // El reporte exporta todo el período: usa el historial completo, nunca el
            // paginado de la pantalla.
            public Task<SIGAC.Application.DTOs.ResultadoPaginado<ItemHistorialDonacion>> ObtenerPaginaHistorialAsync(FiltrosHistorialDonacionDto filtros) => throw new NotImplementedException();
            public Task<IReadOnlyList<SIGAC.Application.DTOs.MontoPorMonedaDto>> ObtenerTotalesDineroPorMonedaAsync(FiltrosHistorialDonacionDto filtros) => throw new NotImplementedException();
        }

        // Solo la consulta del reporte, con los mismos criterios que
        // ProyectosRepositoryEfCore (estado, fecha de inicio con el último día completo
        // y orden de la más reciente a la más antigua).
        private sealed class RepositorioProyectosFalso : IProyectosRepository
        {
            public List<ProyectoComunitario> Proyectos { get; } = new();
            public bool Falla { get; set; }

            public Task<IReadOnlyList<ProyectoComunitario>> ObtenerParaReporteAsync(FiltrosReporteProyectosDto filtros)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                IReadOnlyList<ProyectoComunitario> resultado = Proyectos
                    .Where(p => filtros.Estado is null || p.Estado == filtros.Estado)
                    .Where(p => filtros.FechaDesde is null || p.FechaInicio >= filtros.FechaDesde.Value.Date)
                    .Where(p => filtros.FechaHasta is null || p.FechaInicio < filtros.FechaHasta.Value.Date.AddDays(1))
                    .OrderByDescending(p => p.FechaInicio).ThenByDescending(p => p.Id)
                    .ToList();

                return Task.FromResult(resultado);
            }

            public Task AgregarAsync(ProyectoComunitario proyecto) => throw new NotImplementedException();
            public Task<ProyectoComunitario?> ObtenerPorIdAsync(int id) => throw new NotImplementedException();
            public Task<ProyectoComunitario?> ObtenerConParticipantesAsync(int id) => throw new NotImplementedException();
            public Task ActualizarAsync(ProyectoComunitario proyecto) => throw new NotImplementedException();
            public Task<IEnumerable<ProyectoComunitario>> ObtenerTodosAsync(SIGAC.Application.DTOs.Proyectos.FiltrosProyectoDto filtros) => throw new NotImplementedException();
            public Task FinalizarAsync(int id) => throw new NotImplementedException();
            public Task AgregarParticipanteAsync(ParticipanteProyecto participante) => throw new NotImplementedException();
            public Task<bool> ExisteParticipanteAsync(int proyectoId, int beneficiarioId) => throw new NotImplementedException();
            public Task<bool> QuitarParticipanteAsync(int proyectoId, int participanteId) => throw new NotImplementedException();
        }

        // Solo las dos consultas del historial de movimientos, con el mismo criterio
        // de fechas que InventarioRepositoryEfCore (el último día entra completo). La
        // exclusión de las entradas anuladas vive en la consulta SQL del repositorio
        // real y no se reproduce acá: se verificó en el navegador.
        private sealed class RepositorioInventarioFalso : IInventarioRepository
        {
            public List<EntradaInventario> Entradas { get; } = new();
            public List<SalidaInventario> Salidas { get; } = new();
            public bool Falla { get; set; }

            public Task<IEnumerable<EntradaInventario>> ObtenerEntradasAsync(int? articuloId, DateTime? desde, DateTime? hasta)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                return Task.FromResult<IEnumerable<EntradaInventario>>(Entradas
                    .Where(e => articuloId is null || e.ArticuloId == articuloId)
                    .Where(e => desde is null || e.Fecha >= desde.Value.Date)
                    .Where(e => hasta is null || e.Fecha < hasta.Value.Date.AddDays(1))
                    .ToList());
            }

            public Task<IEnumerable<SalidaInventario>> ObtenerSalidasAsync(int? articuloId, DateTime? desde, DateTime? hasta)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                return Task.FromResult<IEnumerable<SalidaInventario>>(Salidas
                    .Where(s => articuloId is null || s.ArticuloId == articuloId)
                    .Where(s => desde is null || s.Fecha >= desde.Value.Date)
                    .Where(s => hasta is null || s.Fecha < hasta.Value.Date.AddDays(1))
                    .ToList());
            }

            // El reporte exporta todo el período: usa el historial completo, nunca el
            // paginado de la pantalla.
            public Task<SIGAC.Application.DTOs.ResultadoPaginado<ItemMovimiento>> ObtenerPaginaMovimientosAsync(FiltrosMovimientoDto filtros) => throw new NotImplementedException();
            public Task<TotalesMovimientos> ObtenerTotalesMovimientosAsync(FiltrosMovimientoDto filtros) => throw new NotImplementedException();

            // Lo que devuelven las consultas del panorama y con qué parámetros se
            // pidieron: el servicio solo las reparte y suma, así que la prueba controla
            // la entrada.
            public List<UnidadesPorMesYTipoDto> EntradasPorMesYOrigen { get; } = new();
            public List<UnidadesPorMesYTipoDto> SalidasPorMesYTipo { get; } = new();
            public List<MovimientosPorArticuloDto> ArticulosConMasMovimientos { get; } = new();
            public List<(int Meses, int? Maximo)> Peticiones { get; } = new();

            public Task<IReadOnlyList<UnidadesPorMesYTipoDto>> ObtenerEntradasPorMesYOrigenAsync(int mesesHaciaAtras) =>
                Panorama(mesesHaciaAtras, null, EntradasPorMesYOrigen);

            public Task<IReadOnlyList<UnidadesPorMesYTipoDto>> ObtenerSalidasPorMesYTipoAsync(int mesesHaciaAtras) =>
                Panorama(mesesHaciaAtras, null, SalidasPorMesYTipo);

            public Task<IReadOnlyList<MovimientosPorArticuloDto>> ObtenerArticulosConMasMovimientosAsync(int mesesHaciaAtras, int maximo) =>
                Panorama(mesesHaciaAtras, maximo, ArticulosConMasMovimientos);

            private Task<IReadOnlyList<T>> Panorama<T>(int meses, int? maximo, List<T> datos)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                Peticiones.Add((meses, maximo));
                return Task.FromResult<IReadOnlyList<T>>(datos.ToList());
            }

            public Task<IReadOnlyList<Articulo>> ObtenerArticulosPorNombreAsync(string nombre) => throw new NotImplementedException();
            public Task<Articulo?> ObtenerArticuloPorIdAsync(int id) => throw new NotImplementedException();
            public Task ActualizarArticuloAsync(Articulo articulo) => throw new NotImplementedException();
            public Task<SIGAC.Application.DTOs.ResultadoPaginado<Articulo>> ObtenerExistenciasAsync(FiltrosExistenciaDto filtros) => throw new NotImplementedException();
            public Task<bool> ExisteCodigoAsync(string? codigo, int? idExcluir = null) => throw new NotImplementedException();
            public Task<int> ContarStockBajoAsync() => throw new NotImplementedException();
            public Task<bool> TieneMovimientosAsync(int articuloId) => throw new NotImplementedException();
            public Task EliminarArticuloAsync(int articuloId) => throw new NotImplementedException();
            public Task RegistrarEntradaConStockAsync(EntradaInventario entrada, Articulo? articuloNuevo) => throw new NotImplementedException();
            public Task RegistrarSalidaConStockAsync(SalidaInventario salida) => throw new NotImplementedException();
            public Task AprobarPrestamoConStockAsync(SolicitudPrestamo solicitud, SalidaInventario salida) => throw new NotImplementedException();
            public Task AgregarSolicitudPrestamoAsync(SolicitudPrestamo solicitud) => throw new NotImplementedException();
            public Task<SolicitudPrestamo?> ObtenerSolicitudPorIdAsync(int id) => throw new NotImplementedException();
            public Task ActualizarSolicitudAsync(SolicitudPrestamo solicitud) => throw new NotImplementedException();
            public Task<IEnumerable<SolicitudPrestamo>> ObtenerSolicitudesAsync(EstadoSolicitudPrestamo? estado = null) => throw new NotImplementedException();
        }

        // Solo el historial, con los mismos criterios que AlquileresRepositoryEfCore
        // (fechas inclusivas porque Fecha es una columna date, sector entre otros y
        // estado) y su orden por fecha y hora de inicio.
        private sealed class RepositorioAlquileresFalso : IAlquileresRepository
        {
            public List<AlquilerEspacio> Alquileres { get; } = new();
            public bool Falla { get; set; }

            public Task<IReadOnlyList<AlquilerEspacio>> ObtenerHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                IReadOnlyList<AlquilerEspacio> resultado = Alquileres
                    .Where(a => filtros.FechaDesde is null || a.Fecha >= filtros.FechaDesde.Value.Date)
                    .Where(a => filtros.FechaHasta is null || a.Fecha <= filtros.FechaHasta.Value.Date)
                    .Where(a => filtros.EspacioId is null || a.Espacios.Any(e => e.Id == filtros.EspacioId))
                    .Where(a => filtros.Estado is null || a.Estado == filtros.Estado)
                    .OrderBy(a => a.Fecha).ThenBy(a => a.HoraInicio)
                    .ToList();

                return Task.FromResult(resultado);
            }

            // El reporte exporta todo el período: usa el historial completo, nunca el
            // paginado del calendario.
            public Task<SIGAC.Application.DTOs.ResultadoPaginado<AlquilerEspacio>> ObtenerPaginaHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros) => throw new NotImplementedException();
            public Task<IReadOnlyList<SIGAC.Application.DTOs.MontoPorMonedaDto>> ObtenerTotalesPorMonedaAsync(FiltrosHistorialAlquilerDto filtros) => throw new NotImplementedException();

            public Task AgregarAlquilerAsync(AlquilerEspacio alquiler) => throw new NotImplementedException();
            public Task<IReadOnlyList<AlquilerEspacio>> ObtenerChoquesAsync(
                DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin, IReadOnlyCollection<int> espacioIds) => throw new NotImplementedException();
            public Task<AlquilerEspacio?> ObtenerPorIdAsync(int id) => throw new NotImplementedException();
            public Task<bool> CancelarAsync(int id, string motivoCancelacion) => throw new NotImplementedException();

            // Lo que devuelve la consulta del panorama y con cuántos meses se pidió: el
            // servicio solo la agrupa, así que la prueba controla la entrada.
            public List<AlquilerPanoramaDto> Panorama { get; } = new();
            public List<int> MesesPedidos { get; } = new();

            public Task<IReadOnlyList<AlquilerPanoramaDto>> ObtenerParaPanoramaAsync(int mesesHaciaAtras)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                MesesPedidos.Add(mesesHaciaAtras);
                return Task.FromResult<IReadOnlyList<AlquilerPanoramaDto>>(Panorama.ToList());
            }
        }

        // Solo el resumen de activos e inactivos, que es lo que lee el panorama de
        // donaciones.
        private sealed class RepositorioDonantesFalso : IDonantesRepository
        {
            public SIGAC.Application.DTOs.ResumenRegistrosDto Resumen { get; set; } = SIGAC.Application.DTOs.ResumenRegistrosDto.Vacio;

            public Task<SIGAC.Application.DTOs.ResumenRegistrosDto> ObtenerResumenAsync() => Task.FromResult(Resumen);

            public Task AgregarAsync(Donante donante) => throw new NotImplementedException();
            public Task<Donante?> ObtenerPorIdAsync(int id) => throw new NotImplementedException();
            public Task ActualizarAsync(Donante donante) => throw new NotImplementedException();
            public Task<SIGAC.Application.DTOs.ResultadoPaginado<Donante>> ObtenerTodosAsync(FiltrosDonanteDto filtros) => throw new NotImplementedException();
            public Task<bool> ExisteNombreAsync(string nombre, int? idExcluir = null) => throw new NotImplementedException();
            public Task CambiarEstadoAsync(int id, bool estado) => throw new NotImplementedException();
        }

        private sealed class RepositorioBeneficiariosFalso : SIGAC.Application.Interfaces.IBeneficiariosRepository
        {
            public Task AgregarAsync(Beneficiario beneficiario) => throw new NotImplementedException();
            public Task ActualizarAsync(Beneficiario beneficiario) => throw new NotImplementedException();
            public Task<Beneficiario?> ObtenerPorIdAsync(int id) => throw new NotImplementedException();
            public Task<SIGAC.Application.DTOs.Beneficiarios.BeneficiarioCoincidente?> BuscarPorNombresYFechaAsync(string primerNombre, string segundoNombre, string primerApellido, string segundoApellido, DateTime fechaNacimiento, int? idExcluir = null) => throw new NotImplementedException();
            public Task<SIGAC.Application.DTOs.ResultadoPaginado<Beneficiario>> ObtenerPaginaAsync(SIGAC.Application.DTOs.Beneficiarios.FiltrosBeneficiarioDto filtros) => throw new NotImplementedException();
            public Task<SIGAC.Application.DTOs.Beneficiarios.BeneficiarioCoincidente?> BuscarPorNumIdentidadAsync(string? numIdentidad, int? idExcluir = null) => throw new NotImplementedException();
            public Task CambiarEstadoAsync(int id, bool estado) => throw new NotImplementedException();
            public Task<SIGAC.Application.DTOs.ResumenRegistrosDto> ObtenerResumenAsync() => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoPorCategoriaDto>> ObtenerConteoPorCategoriaAsync() => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoPorMesDto>> ObtenerAltasPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
        }
    }
}
