using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieWatch.Repository.Migrations
{
    /// <inheritdoc />
    public partial class MoodPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PresetKey",
                table: "Moods",
                type: "TEXT",
                maxLength: 40,
                nullable: true);
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO Moods (Id, Name, Description, PresetKey, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                VALUES ('AA000000-0000-0000-0000-000000000001', 'Make me laugh', 'Comedy-led picks for tonight.', 'laugh',
                    '2026-09-30 00:00:00+00:00', '2026-09-30 00:00:00+00:00', 'mood-presets', 'mood-presets');
                UPDATE Moods SET PresetKey = 'laugh' WHERE Name = 'Make me laugh' AND PresetKey IS NULL;
                """);
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO Moods (Id, Name, Description, PresetKey, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                VALUES ('AA000000-0000-0000-0000-000000000002', 'Give me suspense', 'Thrillers and mysteries.', 'suspense',
                    '2026-09-30 00:00:00+00:00', '2026-09-30 00:00:00+00:00', 'mood-presets', 'mood-presets');
                UPDATE Moods SET PresetKey = 'suspense' WHERE Name = 'Give me suspense' AND PresetKey IS NULL;
                """);
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO Moods (Id, Name, Description, PresetKey, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                VALUES ('AA000000-0000-0000-0000-000000000003', 'Take me on an adventure', 'Adventure, action and fantasy.', 'adventure',
                    '2026-09-30 00:00:00+00:00', '2026-09-30 00:00:00+00:00', 'mood-presets', 'mood-presets');
                UPDATE Moods SET PresetKey = 'adventure' WHERE Name = 'Take me on an adventure' AND PresetKey IS NULL;
                """);
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO Moods (Id, Name, Description, PresetKey, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                VALUES ('AA000000-0000-0000-0000-000000000004', 'Scare me', 'Horror-led picks for tonight.', 'scare',
                    '2026-09-30 00:00:00+00:00', '2026-09-30 00:00:00+00:00', 'mood-presets', 'mood-presets');
                UPDATE Moods SET PresetKey = 'scare' WHERE Name = 'Scare me' AND PresetKey IS NULL;
                """);
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO Moods (Id, Name, Description, PresetKey, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                VALUES ('AA000000-0000-0000-0000-000000000005', 'Make me think', 'Documentary, drama and science fiction.', 'think',
                    '2026-09-30 00:00:00+00:00', '2026-09-30 00:00:00+00:00', 'mood-presets', 'mood-presets');
                UPDATE Moods SET PresetKey = 'think' WHERE Name = 'Make me think' AND PresetKey IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresetKey",
                table: "Moods");
        }
    }
}
