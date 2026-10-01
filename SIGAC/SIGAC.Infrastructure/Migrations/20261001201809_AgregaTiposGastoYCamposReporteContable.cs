using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregaTiposGastoYCamposReporteContable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BORRADO DELIBERADO DE TODOS LOS GASTOS OPERATIVOS. No es un accidente.
            //
            // Todo lo que había en GastosOperativos eran datos inventados de prueba:
            // la asociación todavía no cargó gastos reales. Con el esquema nuevo cada
            // gasto necesita un tipo de la tabla TiposGasto, proveedor y número de
            // factura, y las categorías viejas (ServiciosBasicos, Transporte,
            // CompraInsumos, Salarios, Viaticos) no se corresponden con los tipos del
            // reporte de la contadora. Inventar un mapeo para datos de prueba no
            // tenía sentido, así que se decidió borrarlos. Si al correr esta migración
            // en tu base local desaparecen tus gastos, es lo esperado.
            //
            // El orden importa: EntradasInventario.GastoOperativoId es una FK con
            // Restrict, así que borrar los gastos sin desvincular antes sus entradas
            // hace fallar la migración a medio camino (error 547). Se anula la
            // referencia y no se borra la entrada, por el mismo criterio que en
            // AddTablaGastosOperativos: su cantidad ya está sumada al StockActual del
            // artículo, y la columna es nullable porque una entrada por compra sin
            // gasto vinculado es válida.
            //
            // Va ANTES de cualquier cambio de esquema: la columna TipoGastoId se
            // agrega como NOT NULL con default 0 y su FK solo se puede crear sobre una
            // tabla vacía.
            migrationBuilder.Sql(@"
                UPDATE EntradasInventario
                SET GastoOperativoId = NULL
                WHERE GastoOperativoId IS NOT NULL;");

            migrationBuilder.Sql("DELETE FROM GastosOperativos;");

            migrationBuilder.DropIndex(
                name: "IX_GastosOperativos_Fecha_Categoria",
                table: "GastosOperativos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GastosOperativos_Categoria",
                table: "GastosOperativos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GastosOperativos_Monto",
                table: "GastosOperativos");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "GastosOperativos");

            migrationBuilder.RenameColumn(
                name: "Monto",
                table: "GastosOperativos",
                newName: "MontoSinIva");

            migrationBuilder.AddColumn<string>(
                name: "CuentaContable",
                table: "GastosOperativos",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: false,
                defaultValue: "1 CAJA Y BANCOS");

            migrationBuilder.AddColumn<string>(
                name: "DescripcionCuenta",
                table: "GastosOperativos",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: false,
                defaultValue: "GASTOS ADMINISTRATIVOS");

            migrationBuilder.AddColumn<string>(
                name: "FormaPago",
                table: "GastosOperativos",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                defaultValue: "Contado");

            migrationBuilder.AddColumn<decimal>(
                name: "Iva",
                table: "GastosOperativos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "NumeroCheque",
                table: "GastosOperativos",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroFactura",
                table: "GastosOperativos",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Proveedor",
                table: "GastosOperativos",
                type: "varchar(150)",
                unicode: false,
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TipoGastoId",
                table: "GastosOperativos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TiposGasto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    GeneraInventario = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CuentaContablePorDefecto = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposGasto", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TiposGasto",
                columns: new[] { "Id", "Activo", "CuentaContablePorDefecto", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "1 CAJA Y BANCOS", "Alquiler de Equipo" },
                    { 2, true, "1 CAJA Y BANCOS", "Amenidades" },
                    { 3, true, "1 CAJA Y BANCOS", "Combustible" },
                    { 4, true, "1 CAJA Y BANCOS", "Mantenimiento de Vehículo" }
                });

            migrationBuilder.InsertData(
                table: "TiposGasto",
                columns: new[] { "Id", "Activo", "CuentaContablePorDefecto", "GeneraInventario", "Nombre" },
                values: new object[] { 5, true, "1 CAJA Y BANCOS", true, "Materiales y Suministros" });

            migrationBuilder.InsertData(
                table: "TiposGasto",
                columns: new[] { "Id", "Activo", "CuentaContablePorDefecto", "Nombre" },
                values: new object[,]
                {
                    { 6, true, "1 CAJA Y BANCOS", "Servicio de Agua" },
                    { 7, true, "1 CAJA Y BANCOS", "Servicio de Cable, Teléfono e Internet" }
                });

            migrationBuilder.InsertData(
                table: "TiposGasto",
                columns: new[] { "Id", "Activo", "CuentaContablePorDefecto", "GeneraInventario", "Nombre" },
                values: new object[] { 8, true, "1 CAJA Y BANCOS", true, "Suministros de Cocina" });

            migrationBuilder.InsertData(
                table: "TiposGasto",
                columns: new[] { "Id", "Activo", "CuentaContablePorDefecto", "Nombre" },
                values: new object[] { 9, true, "1 CAJA Y BANCOS", "Salarios" });

            migrationBuilder.CreateIndex(
                name: "IX_GastosOperativos_Fecha_TipoGasto",
                table: "GastosOperativos",
                columns: new[] { "Fecha", "TipoGastoId" });

            migrationBuilder.CreateIndex(
                name: "IX_GastosOperativos_TipoGasto",
                table: "GastosOperativos",
                column: "TipoGastoId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GastosOperativos_FormaPago",
                table: "GastosOperativos",
                sql: "[FormaPago] IN ('Contado', 'Crédito')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GastosOperativos_Iva",
                table: "GastosOperativos",
                sql: "[Iva] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GastosOperativos_MontoSinIva",
                table: "GastosOperativos",
                sql: "[MontoSinIva] > 0");

            migrationBuilder.CreateIndex(
                name: "UX_TiposGasto_Nombre",
                table: "TiposGasto",
                column: "Nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GastosOperativos_TiposGasto_TipoGastoId",
                table: "GastosOperativos",
                column: "TipoGastoId",
                principalTable: "TiposGasto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mismo borrado que en Up y por el mismo motivo, en sentido inverso: un
            // gasto con tipo, proveedor y factura no tiene categoría equivalente en el
            // esquema anterior, y la columna Categoria vuelve con '' por defecto, que
            // CK_GastosOperativos_Categoria rechazaría al recrearse.
            migrationBuilder.Sql(@"
                UPDATE EntradasInventario
                SET GastoOperativoId = NULL
                WHERE GastoOperativoId IS NOT NULL;");

            migrationBuilder.Sql("DELETE FROM GastosOperativos;");

            migrationBuilder.DropForeignKey(
                name: "FK_GastosOperativos_TiposGasto_TipoGastoId",
                table: "GastosOperativos");

            migrationBuilder.DropTable(
                name: "TiposGasto");

            migrationBuilder.DropIndex(
                name: "IX_GastosOperativos_Fecha_TipoGasto",
                table: "GastosOperativos");

            migrationBuilder.DropIndex(
                name: "IX_GastosOperativos_TipoGasto",
                table: "GastosOperativos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GastosOperativos_FormaPago",
                table: "GastosOperativos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GastosOperativos_Iva",
                table: "GastosOperativos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GastosOperativos_MontoSinIva",
                table: "GastosOperativos");

            migrationBuilder.DropColumn(
                name: "CuentaContable",
                table: "GastosOperativos");

            migrationBuilder.DropColumn(
                name: "DescripcionCuenta",
                table: "GastosOperativos");

            migrationBuilder.DropColumn(
                name: "FormaPago",
                table: "GastosOperativos");

            migrationBuilder.DropColumn(
                name: "Iva",
                table: "GastosOperativos");

            migrationBuilder.DropColumn(
                name: "NumeroCheque",
                table: "GastosOperativos");

            migrationBuilder.DropColumn(
                name: "NumeroFactura",
                table: "GastosOperativos");

            migrationBuilder.DropColumn(
                name: "Proveedor",
                table: "GastosOperativos");

            migrationBuilder.DropColumn(
                name: "TipoGastoId",
                table: "GastosOperativos");

            migrationBuilder.RenameColumn(
                name: "MontoSinIva",
                table: "GastosOperativos",
                newName: "Monto");

            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "GastosOperativos",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_GastosOperativos_Fecha_Categoria",
                table: "GastosOperativos",
                columns: new[] { "Fecha", "Categoria" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_GastosOperativos_Categoria",
                table: "GastosOperativos",
                sql: "[Categoria] IN ('ServiciosBasicos', 'Transporte', 'CompraInsumos', 'Salarios', 'Viaticos')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GastosOperativos_Monto",
                table: "GastosOperativos",
                sql: "[Monto] > 0");
        }
    }
}
