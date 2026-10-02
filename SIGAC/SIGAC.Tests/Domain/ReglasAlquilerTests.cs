using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Domain
{
    public class ReglasAlquilerTests
    {
        // 2026-10-03 es sábado; 2026-10-05, lunes.
        private static readonly DateTime Sabado = new(2026, 10, 3);
        private static readonly DateTime Domingo = new(2026, 10, 4);
        private static readonly DateTime Lunes = new(2026, 10, 5);

        // El horario con el que arranca el sistema (L-V 8-20, S-D 8-17).
        private static readonly HorarioAlquiler Horario = HorarioAlquiler.PorDefecto();

        [Fact]
        public void Un_horario_configurado_distinto_cambia_lo_que_se_permite()
        {
            var extendido = HorarioAlquiler.PorDefecto();
            extendido.CierreFinDeSemana = H(21);

            Assert.False(ReglasAlquiler.EstaDentroDelHorario(Sabado, H(16), H(19), Horario));
            Assert.True(ReglasAlquiler.EstaDentroDelHorario(Sabado, H(16), H(19), extendido));
            Assert.Equal("Sábado y domingo se alquila de 8:00 a. m. a 9:00 p. m.", ReglasAlquiler.DescribirHorario(Sabado, extendido));
        }

        [Fact]
        public void El_fin_de_semana_sale_de_la_fecha()
        {
            Assert.True(ReglasAlquiler.EsFinDeSemana(Sabado));
            Assert.True(ReglasAlquiler.EsFinDeSemana(Domingo));
            Assert.False(ReglasAlquiler.EsFinDeSemana(Lunes));
        }

        [Fact]
        public void Entre_semana_se_alquila_de_8_a_20_y_el_fin_de_semana_de_8_a_17()
        {
            Assert.Equal((new TimeSpan(8, 0, 0), new TimeSpan(20, 0, 0)), ReglasAlquiler.HorarioPermitido(Lunes, Horario));
            Assert.Equal((new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)), ReglasAlquiler.HorarioPermitido(Sabado, Horario));
        }

        [Theory]
        [InlineData(8, 17, true)]
        [InlineData(16, 18, false)]
        [InlineData(7, 9, false)]
        public void El_sabado_no_se_pasa_de_las_17(int inicio, int fin, bool permitido)
        {
            Assert.Equal(permitido, ReglasAlquiler.EstaDentroDelHorario(Sabado, H(inicio), H(fin), Horario));
        }

        [Fact]
        public void La_misma_franja_que_el_sabado_se_rechaza_si_el_lunes_la_permite()
        {
            Assert.True(ReglasAlquiler.EstaDentroDelHorario(Lunes, H(16), H(19), Horario));
            Assert.False(ReglasAlquiler.EstaDentroDelHorario(Sabado, H(16), H(19), Horario));
        }

        [Theory]
        [InlineData(10, 12, 11, 13, true)]   // se superponen
        [InlineData(10, 14, 11, 12, true)]   // una contiene a la otra
        [InlineData(10, 12, 12, 14, false)]  // seguidas: una termina cuando empieza la otra
        [InlineData(10, 12, 13, 14, false)]  // separadas
        public void Traslape_de_franjas(int inicioA, int finA, int inicioB, int finB, bool chocan)
        {
            Assert.Equal(chocan, ReglasAlquiler.SeTraslapan(H(inicioA), H(finA), H(inicioB), H(finB)));
            Assert.Equal(chocan, ReglasAlquiler.SeTraslapan(H(inicioB), H(finB), H(inicioA), H(finA)));
        }

        [Fact]
        public void Se_puede_reservar_para_hoy_pero_no_para_ayer()
        {
            var hoy = new DateTime(2026, 10, 1);

            Assert.True(ReglasAlquiler.EsFechaValidaParaReservar(hoy, hoy));
            Assert.True(ReglasAlquiler.EsFechaValidaParaReservar(hoy.AddDays(10), hoy));
            Assert.False(ReglasAlquiler.EsFechaValidaParaReservar(hoy.AddDays(-1), hoy));
        }

        [Fact]
        public void La_descripcion_del_horario_dice_cual_aplica()
        {
            Assert.Equal("Sábado y domingo se alquila de 8:00 a. m. a 5:00 p. m.", ReglasAlquiler.DescribirHorario(Sabado, Horario));
            Assert.Equal("De lunes a viernes se alquila de 8:00 a. m. a 8:00 p. m.", ReglasAlquiler.DescribirHorario(Lunes, Horario));
        }

        [Theory]
        [InlineData(0, 0, "12:00 a. m.")]   // medianoche
        [InlineData(8, 0, "8:00 a. m.")]
        [InlineData(11, 45, "11:45 a. m.")]
        [InlineData(12, 0, "12:00 p. m.")]  // mediodía
        [InlineData(17, 30, "5:30 p. m.")]
        [InlineData(23, 15, "11:15 p. m.")]
        public void Las_horas_se_muestran_en_12_horas_con_a_m_y_p_m(int hora, int minutos, string esperado)
        {
            Assert.Equal(esperado, ReglasAlquiler.FormatearHora(new TimeSpan(hora, minutos, 0)));
        }

        private static TimeSpan H(int hora) => new(hora, 0, 0);
    }
}
