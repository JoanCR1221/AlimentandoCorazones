namespace SIGAC.Domain
{
    // Tiempos de comida válidos: lista cerrada. Fuente única para el servicio y la
    // UI, igual que CategoriasBeneficiario y TiposDocumento. Los valores estaban
    // repetidos como literales en cada pantalla y en la validación, que es lo que
    // dejó pasar "Cena" en su momento.
    public static class TiemposComida
    {
        public const string Desayuno = "Desayuno";
        public const string Almuerzo = "Almuerzo";
        public const string Merienda = "Merienda";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Desayuno,
            Almuerzo,
            Merienda
        };

        public static bool EsValido(string? tiempoComida) =>
            tiempoComida is not null && Todos.Contains(tiempoComida);
    }
}
