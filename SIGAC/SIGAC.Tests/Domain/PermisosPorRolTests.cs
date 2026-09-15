using SIGAC.Domain;

namespace SIGAC.Tests.Domain
{
    public class PermisosPorRolTests
    {
        [Fact]
        public void Administrador_tiene_todos_los_permisos_del_catalogo()
        {
            var permisos = PermisosPorRol.Obtener(RolesSistema.Administrador);

            Assert.Equal(Permisos.Todos.OrderBy(p => p), permisos.OrderBy(p => p));
        }

        [Fact]
        public void Colaborador_tiene_todos_los_modulos_operativos_pero_no_seguridad()
        {
            var permisos = PermisosPorRol.Obtener(RolesSistema.Colaborador);

            Assert.DoesNotContain(Permisos.Seguridad.GestionarUsuarios, permisos);
            Assert.DoesNotContain(Permisos.Seguridad.VerBitacora, permisos);

            var operativos = Permisos.Definiciones
                .Where(p => p.Modulo != ModulosSistema.Seguridad)
                .Select(p => p.Clave);

            Assert.Equal(operativos.OrderBy(p => p), permisos.OrderBy(p => p));
        }

        [Fact]
        public void Asistente_solo_registra_asistencia_y_entradas_de_inventario()
        {
            var permisos = PermisosPorRol.Obtener(RolesSistema.Asistente);

            Assert.Equal(
                new[] { Permisos.Asistencia.Registrar, Permisos.Inventario.RegistrarEntrada }.OrderBy(p => p),
                permisos.OrderBy(p => p));
        }

        [Fact]
        public void Asistente_no_tiene_ningun_permiso_de_consulta()
        {
            var permisos = PermisosPorRol.Obtener(RolesSistema.Asistente);

            Assert.DoesNotContain(Permisos.Asistencia.Ver, permisos);
            Assert.DoesNotContain(Permisos.Inventario.VerExistencias, permisos);
            Assert.DoesNotContain(Permisos.Beneficiarios.Ver, permisos);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Invitado")]
        public void Rol_desconocido_no_tiene_permisos(string? rol)
        {
            Assert.Empty(PermisosPorRol.Obtener(rol));
            Assert.Empty(PermisosPorRol.CalcularEfectivos(rol, new[] { Permisos.Gastos.Ver }));
        }

        [Fact]
        public void Todo_rol_valido_tiene_al_menos_un_permiso()
        {
            foreach (var rol in RolesSistema.Todos)
                Assert.NotEmpty(PermisosPorRol.Obtener(rol));
        }

        [Fact]
        public void Rol_por_defecto_es_el_de_menos_privilegios()
        {
            var cantidadPorRol = RolesSistema.Todos
                .ToDictionary(r => r, r => PermisosPorRol.Obtener(r).Count);

            Assert.Equal(RolesSistema.PorDefecto, cantidadPorRol.MinBy(kv => kv.Value).Key);
        }

        [Fact]
        public void Calcular_efectivos_quita_los_revocados_del_rol()
        {
            var efectivos = PermisosPorRol.CalcularEfectivos(
                RolesSistema.Colaborador,
                new[] { Permisos.Gastos.Anular, Permisos.Beneficiarios.CambiarEstado });

            Assert.DoesNotContain(Permisos.Gastos.Anular, efectivos);
            Assert.DoesNotContain(Permisos.Beneficiarios.CambiarEstado, efectivos);
            Assert.Contains(Permisos.Gastos.Ver, efectivos);
            Assert.Equal(PermisosPorRol.Obtener(RolesSistema.Colaborador).Count - 2, efectivos.Count);
        }

        [Fact]
        public void Calcular_efectivos_ignora_las_revocaciones_del_administrador()
        {
            var efectivos = PermisosPorRol.CalcularEfectivos(
                RolesSistema.Administrador,
                new[] { Permisos.Seguridad.GestionarUsuarios, Permisos.Gastos.Ver });

            Assert.Equal(Permisos.Todos.OrderBy(p => p), efectivos.OrderBy(p => p));
        }

        [Fact]
        public void Calcular_efectivos_nunca_agrega_permisos_ajenos_al_rol()
        {
            // Una revocación de un permiso que el rol no tiene (por ejemplo, quedó
            // guardada de cuando el usuario era Colaborador) no debe aparecer ni
            // como concedido ni romper el cálculo.
            var efectivos = PermisosPorRol.CalcularEfectivos(
                RolesSistema.Asistente,
                new[] { Permisos.Gastos.Ver, Permisos.Asistencia.Registrar });

            Assert.Equal(new[] { Permisos.Inventario.RegistrarEntrada }, efectivos);
        }

        [Fact]
        public void Calcular_efectivos_sin_revocados_devuelve_los_del_rol()
        {
            Assert.Equal(
                PermisosPorRol.Obtener(RolesSistema.Colaborador),
                PermisosPorRol.CalcularEfectivos(RolesSistema.Colaborador, null));
        }

        [Fact]
        public void Solo_colaborador_y_asistente_admiten_revocaciones()
        {
            Assert.False(PermisosPorRol.AdmiteRevocaciones(RolesSistema.Administrador));
            Assert.True(PermisosPorRol.AdmiteRevocaciones(RolesSistema.Colaborador));
            Assert.True(PermisosPorRol.AdmiteRevocaciones(RolesSistema.Asistente));
            Assert.False(PermisosPorRol.AdmiteRevocaciones(null));
            Assert.False(PermisosPorRol.AdmiteRevocaciones("Invitado"));
        }

        [Fact]
        public void Revocaciones_invalidas_detecta_las_que_el_rol_no_incluye()
        {
            var invalidas = PermisosPorRol.RevocacionesInvalidas(
                RolesSistema.Asistente,
                new[] { Permisos.Asistencia.Registrar, Permisos.Gastos.Ver, Permisos.Gastos.Ver, "NoExiste" });

            Assert.Equal(new[] { Permisos.Gastos.Ver, "NoExiste" }, invalidas);
        }
    }

    public class PermisosTests
    {
        [Fact]
        public void Las_claves_del_catalogo_son_unicas()
        {
            Assert.Equal(Permisos.Todos.Count, Permisos.Todos.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void Toda_clave_empieza_por_su_modulo()
        {
            foreach (var permiso in Permisos.Definiciones)
            {
                Assert.True(ModulosSistema.EsValido(permiso.Modulo), $"{permiso.Clave} tiene módulo inválido");
                Assert.StartsWith(permiso.Modulo + ".", permiso.Clave, StringComparison.Ordinal);
                Assert.False(string.IsNullOrWhiteSpace(permiso.Descripcion), $"{permiso.Clave} sin descripción");
            }
        }

        [Fact]
        public void Es_valido_reconoce_claves_del_catalogo_y_rechaza_el_resto()
        {
            Assert.True(Permisos.EsValido(Permisos.Seguridad.VerBitacora));
            Assert.False(Permisos.EsValido("Seguridad.Otro"));
            Assert.False(Permisos.EsValido(null));
        }

        [Fact]
        public void Del_modulo_devuelve_solo_los_permisos_de_ese_modulo()
        {
            var deSeguridad = Permisos.DelModulo(ModulosSistema.Seguridad);

            Assert.Equal(
                new[] { Permisos.Seguridad.GestionarUsuarios, Permisos.Seguridad.VerBitacora },
                deSeguridad);
        }
    }

    public class RolesSistemaTests
    {
        [Fact]
        public void Hay_exactamente_tres_roles_y_el_por_defecto_es_uno_de_ellos()
        {
            Assert.Equal(3, RolesSistema.Todos.Count);
            Assert.Contains(RolesSistema.PorDefecto, RolesSistema.Todos);
            Assert.Equal(RolesSistema.Asistente, RolesSistema.PorDefecto);
        }

        [Theory]
        [InlineData("Administrador", true)]
        [InlineData("administrador", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Es_valido_distingue_mayusculas(string? rol, bool esperado)
        {
            Assert.Equal(esperado, RolesSistema.EsValido(rol));
        }
    }
}
