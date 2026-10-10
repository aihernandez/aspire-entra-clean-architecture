using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeUserList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_EntraTenantId_FirstName_LastName_Id",
                schema: "dbo",
                table: "Users",
                columns: new[] { "EntraTenantId", "FirstName", "LastName", "Id" })
                .Annotation("SqlServer:Include", new[] { "Email", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_EntraTenantId_FirstName_LastName_Id",
                schema: "dbo",
                table: "Users");
        }
    }
}
