using System.Globalization;

namespace SIGAC.Components.Shared
{
    // Cultura de los selectores de fecha y de hora (MudDatePicker, MudDateRangePicker
    // y MudTimePicker). Sin esto toman la del servidor: en un servidor en inglés
    // (el de Azure) el calendario sale con "Sun Mon Tue" y "October 2026". Se fija
    // solo en los selectores y no en toda la aplicación, para no cambiar de paso
    // cómo se leen y se escriben los números y los montos.
    public static class CulturaSigac
    {
        public static readonly CultureInfo EsCr = CultureInfo.GetCultureInfo("es-CR");
    }
}
