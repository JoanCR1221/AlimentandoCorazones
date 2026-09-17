using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTablasPermisosRevocadosYBitacora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bitacora",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    NombreUsuario = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                    Rol = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Accion = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Modulo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Detalle = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bitacora", x => x.Id);
                    table.CheckConstraint("CK_Bitacora_Accion", "[Accion] IN ('IniciarSesion', 'IniciarSesionFallido', 'CerrarSesion', 'AccesoDenegado', 'Registrar', 'Editar', 'Eliminar', 'Anular', 'Activar', 'Desactivar', 'Aprobar', 'Rechazar', 'Finalizar', 'Entregar', 'CambiarRol', 'CambiarPermisos', 'CambiarPassword', 'RestablecerPassword')");
                    table.CheckConstraint("CK_Bitacora_Modulo", "[Modulo] IN ('Beneficiarios', 'Asistencia', 'Inventario', 'Donaciones', 'Gastos', 'Proyectos', 'Seguridad')");
                    table.CheckConstraint("CK_Bitacora_Rol", "[Rol] IS NULL OR [Rol] IN ('Administrador', 'Colaborador', 'Asistente')");
                    table.ForeignKey(
                        name: "FK_Bitacora_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PermisosRevocados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Permiso = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisosRevocados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermisosRevocados_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_Fecha",
                table: "Bitacora",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_Modulo",
                table: "Bitacora",
                column: "Modulo");

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_Usuario",
                table: "Bitacora",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "UX_PermisosRevocados_Usuario_Permiso",
                table: "PermisosRevocados",
                columns: new[] { "UsuarioId", "Permiso" },
                unique: true);

            // La bitácora es de solo inserción (PBI 1949: nadie puede editarla ni
            // borrarla). Un trigger INSTEAD OF y no un REVOKE/DENY porque los
            // permisos se otorgan a un login concreto, y hoy la aplicación entra con
            // la cuenta de Windows del desarrollador (sysadmin, a quien no se le
            // puede denegar nada). El trigger aplica a cualquier conexión, incluida
            // esa, y sigue valiendo cuando en producción haya un login propio.
            //
            // Sin ROLLBACK explícito: un THROW dentro de un trigger aborta el batch y
            // deshace la transacción por sí solo.
            migrationBuilder.Sql(@"
CREATE TRIGGER TR_Bitacora_SoloInsercion
ON Bitacora
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50001, 'La bitácora es de solo inserción: no se puede modificar ni eliminar ninguna fila.', 1;
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bitacora");

            migrationBuilder.DropTable(
                name: "PermisosRevocados");
        }
    }
}
