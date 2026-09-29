using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieWatch.Repository.Migrations;

public partial class ReplaceLegacyMoods : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT OR IGNORE INTO Moods (Id, Name, Description, PresetKey, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
            VALUES ('AA000000-0000-0000-0000-000000000006', 'Pick a family film', 'Family and animation genre picks.', 'family',
                '2026-09-30 00:00:00+00:00', '2026-09-30 00:00:00+00:00', 'mood-presets', 'mood-presets');
            UPDATE Moods SET PresetKey = 'family' WHERE Name = 'Pick a family film' AND PresetKey IS NULL;
            """);
        migrationBuilder.Sql("""
            INSERT OR IGNORE INTO Moods (Id, Name, Description, PresetKey, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
            VALUES ('AA000000-0000-0000-0000-000000000007', 'Share a love story', 'Romance-led picks for tonight.', 'romance',
                '2026-09-30 00:00:00+00:00', '2026-09-30 00:00:00+00:00', 'mood-presets', 'mood-presets');
            UPDATE Moods SET PresetKey = 'romance' WHERE Name = 'Share a love story' AND PresetKey IS NULL;
            """);
        migrationBuilder.Sql("""
            INSERT OR IGNORE INTO Moods (Id, Name, Description, PresetKey, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
            VALUES ('AA000000-0000-0000-0000-000000000008', 'Follow a crime story', 'Crime, mystery and thriller films.', 'crime',
                '2026-09-30 00:00:00+00:00', '2026-09-30 00:00:00+00:00', 'mood-presets', 'mood-presets');
            UPDATE Moods SET PresetKey = 'crime' WHERE Name = 'Follow a crime story' AND PresetKey IS NULL;
            """);
        migrationBuilder.Sql("""
            INSERT OR IGNORE INTO Moods (Id, Name, Description, PresetKey, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
            VALUES ('AA000000-0000-0000-0000-000000000009', 'Step back in time', 'History, war and western films.', 'history',
                '2026-09-30 00:00:00+00:00', '2026-09-30 00:00:00+00:00', 'mood-presets', 'mood-presets');
            UPDATE Moods SET PresetKey = 'history' WHERE Name = 'Step back in time' AND PresetKey IS NULL;
            """);
        migrationBuilder.Sql("""
            INSERT OR IGNORE INTO Moods (Id, Name, Description, PresetKey, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
            VALUES ('AA000000-0000-0000-0000-000000000010', 'Feel the music', 'Music-led films and performances.', 'music',
                '2026-09-30 00:00:00+00:00', '2026-09-30 00:00:00+00:00', 'mood-presets', 'mood-presets');
            UPDATE Moods SET PresetKey = 'music' WHERE Name = 'Feel the music' AND PresetKey IS NULL;
            """);
        migrationBuilder.Sql("""
            INSERT OR IGNORE INTO Moods (Id, Name, Description, PresetKey, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
            VALUES ('AA000000-0000-0000-0000-000000000011', 'Have a TV movie night', 'Films made for television.', 'tv',
                '2026-09-30 00:00:00+00:00', '2026-09-30 00:00:00+00:00', 'mood-presets', 'mood-presets');
            UPDATE Moods SET PresetKey = 'tv' WHERE Name = 'Have a TV movie night' AND PresetKey IS NULL;
            """);

        // Only the original named moods are retired. Preserve later custom moods and moods
        // already assigned a preset. If a destination preference exists, it takes precedence.
        var replacements = new (string Name, string PresetKey)[]
        {
            ("Relaxing", "laugh"),
            ("Adventurous", "adventure"),
            ("Thoughtful", "think"),
            ("Excited", "adventure"),
            ("Cheerful", "laugh"),
            ("Romantic", "romance"),
            ("Nostalgic", "history"),
            ("Curious", "think"),
            ("Inspired", "think"),
            ("Emotional", "romance"),
            ("Cozy", "family"),
            ("Playful", "family"),
            ("Suspenseful", "suspense"),
            ("Scared", "scare"),
            ("Escapist", "adventure"),
            ("Focused", "crime"),
            ("Melancholic", "think"),
            ("Energetic", "music"),
            ("Hopeful", "family"),
            ("Intrigued", "suspense"),
        };
        foreach (var (name, presetKey) in replacements)
        {
            migrationBuilder.Sql($"""
                UPDATE OR IGNORE GenrePreferences
                SET MoodId = (SELECT Id FROM Moods WHERE PresetKey = '{presetKey}' ORDER BY Id LIMIT 1)
                WHERE MoodId IN (SELECT Id FROM Moods WHERE Name = '{name}' AND PresetKey IS NULL)
                    AND EXISTS (SELECT 1 FROM Moods WHERE PresetKey = '{presetKey}');
                DELETE FROM GenrePreferences
                WHERE MoodId IN (SELECT Id FROM Moods WHERE Name = '{name}' AND PresetKey IS NULL);
                DELETE FROM Moods WHERE Name = '{name}' AND PresetKey IS NULL;
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException("Retired moods and merged preferences cannot be reconstructed. Restore a database backup to undo this data migration.");
    }
}
