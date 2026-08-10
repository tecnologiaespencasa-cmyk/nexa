using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfilePhotoCropSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProfilePhotoHorizontalPosition",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "ProfilePhotoVerticalPosition",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfilePhotoZoom",
                table: "Users",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 1m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfilePhotoHorizontalPosition",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoVerticalPosition",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoZoom",
                table: "Users");
        }
    }
}
