using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSensorReadingCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_AdapterId_TagAddress_RecordedAt",
                table: "SensorReadings",
                columns: new[] { "AdapterId", "TagAddress", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SensorReadings_AdapterId_TagAddress_RecordedAt",
                table: "SensorReadings");
        }
    }
}
