using System.Text.RegularExpressions;

namespace SIGAC.Components.Shared
{
    // Tabla de migas de pan: una entrada por ruta declarada en un @page de
    // Components/Pages (las que se ven dentro de MainLayout). Sigue el mismo
    // agrupamiento que NavMenu.razor (por ejemplo, las entregas de donación
    // cuelgan de "Beneficiarios", no de "Donaciones", porque ahí las agrupa el
    // menú) para que el menú y las migas cuenten la misma historia.
    //
    // Es solo lectura de presentación: una ruta que falte acá simplemente no
    // muestra migas, no rompe la pantalla.
    public static class RutasSigac
    {
        public sealed record Miga(string Texto, string? Href);

        private sealed record Entrada(Regex Patron, IReadOnlyList<Miga> Migas);

        private static readonly IReadOnlyList<Entrada> Tabla = Construir();

        public static IReadOnlyList<Miga>? MigasPara(string rutaRelativa)
        {
            var sinConsulta = rutaRelativa.Split('?', '#')[0];
            var ruta = "/" + sinConsulta.Trim('/');

            foreach (var entrada in Tabla)
            {
                if (entrada.Patron.IsMatch(ruta))
                    return entrada.Migas;
            }

            return null;
        }

        private static List<Entrada> Construir()
        {
            var beneficiarios = new Miga("Beneficiarios", "/beneficiarios");
            var donantes = new Miga("Donantes", "/donantes");
            var asistencia = new Miga("Asistencia", "/asistencia/historial");
            var gastos = new Miga("Gastos Operativos", "/gastos");
            var inventario = new Miga("Inventario", "/inventario/existencias");
            var proyectos = new Miga("Proyectos", "/proyectos");
            var seguridad = new Miga("Seguridad", "/usuarios");

            var plantillas = new (string Plantilla, Miga[] Migas)[]
            {
                ("/beneficiarios", new[] { new Miga("Beneficiarios", null) }),
                ("/beneficiarios/registrar", new[] { beneficiarios, new Miga("Registrar beneficiario", null) }),
                ("/beneficiarios/editar/{id}", new[] { beneficiarios, new Miga("Editar beneficiario", null) }),

                ("/asistencia/historial", new[] { new Miga("Asistencia", null) }),
                ("/asistencia/registrar", new[] { asistencia, new Miga("Registrar asistencia", null) }),

                ("/donantes", new[] { new Miga("Donantes", null) }),
                ("/donantes/registrar", new[] { donantes, new Miga("Registrar donante", null) }),
                ("/donantes/editar/{id}", new[] { donantes, new Miga("Editar donante", null) }),

                ("/donaciones/historial", new[] { new Miga("Donaciones", null) }),
                ("/donaciones/dinero/registrar", new[] { new Miga("Donaciones", "/donaciones/historial"), new Miga("Donación en dinero", null) }),
                ("/donaciones/especie/registrar", new[] { new Miga("Donaciones", "/donaciones/historial"), new Miga("Donación en especie", null) }),

                // Mismo agrupamiento que NavMenu: la entrega de una donación es la
                // salida hacia un beneficiario, no el alta de la donación en sí.
                ("/donaciones/entregas/registrar", new[] { beneficiarios, new Miga("Entregar donación", null) }),
                ("/donaciones/entregas/historial", new[] { beneficiarios, new Miga("Historial de entregas", null) }),

                ("/gastos", new[] { new Miga("Gastos Operativos", null) }),
                ("/gastos/registrar", new[] { gastos, new Miga("Registrar gasto", null) }),
                ("/gastos/editar/{id}", new[] { gastos, new Miga("Editar gasto", null) }),

                ("/inventario/existencias", new[] { new Miga("Inventario", null) }),
                ("/inventario/entradas/registrar", new[] { inventario, new Miga("Registrar entrada", null) }),
                ("/inventario/prestamos/solicitar", new[] { inventario, new Miga("Solicitar préstamo", null) }),
                ("/inventario/prestamos/gestionar", new[] { inventario, new Miga("Gestionar solicitudes", null) }),
                ("/inventario/movimientos/historial", new[] { inventario, new Miga("Historial de movimientos", null) }),
                ("/inventario/articulos/editar/{id}", new[] { inventario, new Miga("Editar artículo", null) }),

                ("/proyectos", new[] { new Miga("Proyectos", null) }),
                ("/proyectos/registrar", new[] { proyectos, new Miga("Registrar proyecto", null) }),
                ("/proyectos/editar/{id}", new[] { proyectos, new Miga("Editar proyecto", null) }),
                ("/proyectos/{id}/participantes/registrar", new[] { proyectos, new Miga("Registrar participante", null) }),

                ("/usuarios", new[] { new Miga("Seguridad", null) }),
                ("/usuarios/registrar", new[] { seguridad, new Miga("Registrar usuario", null) }),
                ("/usuarios/{id}/permisos", new[] { seguridad, new Miga("Permisos", null) }),
                ("/bitacora", new[] { seguridad, new Miga("Bitácora", null) }),

                ("/configuracion", new[] { new Miga("Configuración", null) }),
            };

            return plantillas
                .Select(p => new Entrada(CompilarPatron(p.Plantilla), p.Migas))
                .ToList();
        }

        // Convierte "/proyectos/{id}/participantes/registrar" en un regex que
        // acepta cualquier valor en el segmento de parámetro. El marcador es un
        // carácter de uso privado de Unicode: no puede aparecer en una URL real,
        // así que Regex.Escape nunca lo toca y se sustituye después sin ambigüedad.
        private static Regex CompilarPatron(string plantilla)
        {
            const string marcador = "";
            var conMarcador = Regex.Replace(plantilla, "\\{[^}]+\\}", marcador);
            var escapado = Regex.Escape(conMarcador).Replace(marcador, "[^/]+");
            return new Regex("^" + escapado + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }
}
