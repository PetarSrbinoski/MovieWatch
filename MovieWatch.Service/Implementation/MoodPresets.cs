using MovieWatch.Domain.Common;
using MovieWatch.Domain.Dto;
using MovieWatch.Domain.Models;

namespace MovieWatch.Service.Implementation;

public static class MoodPresets
{
    public static IReadOnlyList<MoodPresetDto> All { get; } =
    [
        new("laugh", "Make me laugh", "Comedy-led picks for tonight.", [new("Comedy", 35, 2)]),
        new("suspense", "Give me suspense", "Thrillers and mysteries.", [new("Thriller", 53, 2), new("Mystery", 9648, 2)]),
        new("adventure", "Take me on an adventure", "Adventure, action and fantasy.", [new("Adventure", 12, 2), new("Action", 28, 1), new("Fantasy", 14, 1)]),
        new("scare", "Scare me", "Horror-led picks for tonight.", [new("Horror", 27, 2)]),
        new("think", "Make me think", "Documentary, drama and science fiction.", [new("Documentary", 99, 2), new("Drama", 18, 1), new("Science Fiction", 878, 1)]),
        new("family", "Pick a family film", "Family and animation genre picks.", [new("Family", 10751, 2), new("Animation", 16, 2)]),
        new("romance", "Share a love story", "Romance-led picks for tonight.", [new("Romance", 10749, 2)]),
        new("crime", "Follow a crime story", "Crime, mystery and thriller films.", [new("Crime", 80, 2), new("Mystery", 9648, 1), new("Thriller", 53, 1)]),
        new("history", "Step back in time", "History, war and western films.", [new("History", 36, 2), new("War", 10752, 1), new("Western", 37, 1)]),
        new("music", "Feel the music", "Music-led films and performances.", [new("Music", 10402, 2)]),
        new("tv", "Have a TV movie night", "Films made for television.", [new("TV Movie", 10770, 2)])
    ];

    public static string? ValidateKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return All.Any(p => p.Key == key) ? key
            : throw new OperationException(FailureKind.Validation, "Unknown mood preset.");
    }

    public static MoodDto ToDto(Mood mood)
    {
        return new(mood.Id, mood.Name, mood.Description, mood.PresetKey,
            All.SingleOrDefault(p => p.Key == mood.PresetKey)?.DefaultWeights ?? []);
    }

    public static int Weight(MoodDto mood, Genre genre)
    {
        return mood.DefaultWeights?.FirstOrDefault(w =>
            genre.TmdbId is { } id ? w.TmdbId == id : string.Equals(w.GenreName, genre.Name, StringComparison.OrdinalIgnoreCase))?.Weight ?? 0;
    }
}
