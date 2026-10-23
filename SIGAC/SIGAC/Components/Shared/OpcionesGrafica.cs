namespace SIGAC.Components.Shared
{
    // Ajustes comunes de las gráficas de los panoramas.
    public static class OpcionesGrafica
    {
        // MudChart arma el eje Y en pasos de YAxisTicks, 20 por defecto (pensado para
        // montos): con conteos de uno o dos dígitos el eje iba de 0 a 20 y las barras
        // quedaban aplastadas abajo. Se elige un paso chico según el valor máximo,
        // para que el eje tenga unas siete marcas como mucho.
        public static int PasoDeConteo(double maximo) => maximo switch
        {
            <= 6 => 1,
            <= 12 => 2,
            <= 30 => 5,
            <= 60 => 10,
            _ => 20
        };

        // Etiquetas de mes ("Ene 2026") inclinadas, como los nombres largos de
        // proveedor, donante o artículo. Las gráficas se dibujan al ancho real
        // (MatchBoundsToSize): en un teléfono quedan unos 20px por mes y las doce
        // etiquetas derechas se montaban unas sobre otras.
        public const int RotacionEtiquetasMes = 45;
    }
}
