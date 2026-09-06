namespace SIGAC.Domain.Entities
{
    // Donación que sale de la asociación hacia un beneficiario o una comunidad.
    //
    // Guarda el "quién y por qué" de la entrega. El "cuánto salió y cuándo" lo
    // sigue registrando Inventario en SalidaInventario (tipo "Donacion"), que
    // apuntará a esta donación igual que hoy apunta a una solicitud con
    // SolicitudPrestamoId: Inventario sigue siendo el dueño del stock y el servicio
    // de Donaciones delega en IInventarioService para descontarlo.
    public class DonacionEntregada
    {
        public int Id { get; set; }

        // Aquí sí es un ArticuloId y no texto libre (a diferencia de
        // DetalleDonacionEspecie): solo se puede entregar algo que ya está en el
        // inventario, porque de ahí se descuenta.
        public int ArticuloId { get; set; }
        public Articulo? Articulo { get; set; }

        public int Cantidad { get; set; }
        public DateTime Fecha { get; set; }

        // Discriminador: valor del catálogo cerrado TiposDestinatarioDonacion.
        // Decide cuál de los dos campos de destinatario se usa.
        public string TipoDestinatario { get; set; } = string.Empty;

        // BeneficiarioId y ComunidadDestinataria son ambos nullable y mutuamente
        // excluyentes porque el destinatario es de uno u otro tipo, nunca de los
        // dos: si TipoDestinatario es "Beneficiario" se llena BeneficiarioId y
        // ComunidadDestinataria queda en NULL; si es "Comunidad" ocurre lo
        // contrario. Se modela así, y no con dos entidades separadas, porque el
        // resto de la entrega (artículo, cantidad, fecha) es idéntico en ambos
        // casos.
        //
        // Una comunidad es texto libre y no una clave foránea por la misma razón
        // que Articulo.Ubicacion: SIGAC todavía no modela comunidades como entidad
        // propia. Un beneficiario sí está registrado, así que ese lado sí es FK.
        //
        // El nullable es lo único que la base puede exigir; que se llene el campo
        // correcto según el tipo es responsabilidad del servicio.
        public int? BeneficiarioId { get; set; }
        public Beneficiario? Beneficiario { get; set; }
        public string? ComunidadDestinataria { get; set; }

        public string? Observaciones { get; set; }
    }
}
