using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LisBlanc.AdminPanel.Migrations
{
    /// <inheritdoc />
    public partial class AddUserToAppointmentRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "AppointmentRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRequests_UserId",
                table: "AppointmentRequests",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentRequests_Users_UserId",
                table: "AppointmentRequests",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentRequests_Users_UserId",
                table: "AppointmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentRequests_UserId",
                table: "AppointmentRequests");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AppointmentRequests");
        }
    }
}
