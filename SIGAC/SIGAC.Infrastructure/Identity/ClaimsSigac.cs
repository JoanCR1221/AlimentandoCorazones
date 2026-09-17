namespace SIGAC.Infrastructure.Identity
{
    // Tipos de claim propios que viajan en la cookie de sesión, además de los que
    // pone Identity (NameIdentifier = Id, Name = correo, Role = rol,
    // SecurityStamp). Prefijo "sigac:" para que no choquen con ninguno estándar.
    public static class ClaimsSigac
    {
        // Nombre para mostrar (UsuarioSigac.Nombre), para no consultar la base en
        // cada render del AppBar.
        public const string Nombre = "sigac:nombre";

        // Un claim por permiso efectivo (rol menos revocados). Las policies de
        // autorización exigen RequireClaim(Permiso, "<clave>"), así que la
        // pantalla y el menú no vuelven a la base para saber qué mostrar: todo
        // está en la cookie hasta que la revalidación la invalide.
        public const string Permiso = "sigac:permiso";
    }
}
