using MovieWatch.Domain.Common;
using MovieWatch.Domain.Dto;
using MovieWatch.Domain.Models;
using MovieWatch.Repository.Interface;
using MovieWatch.Service.Interface;
using MovieWatch.Service.Mapper;

namespace MovieWatch.Service.Implementation;

public sealed class CatalogueService(
    IRepository<Genre> genres, IRepository<Mood> moods, IRepository<Movie> movies,
    IRepository<GenrePreference> preferences, IRepository<WatchlistEntry> watchlist) : ICatalogueService
{
    public async Task<List<GenreDto>> ListGenresAsync(int skip, int take, CancellationToken cancellationToken)
        => (await genres.PageAsync(ValidateSkip(skip), ValidateTake(take), cancellationToken))
            .Select(ToDto).ToList();

    public async Task<GenreDto> GetGenreAsync(Guid id, CancellationToken cancellationToken)
        => ToDto(await RequireGenre(id, cancellationToken));

    public async Task<GenreDto> CreateGenreAsync(string name, CancellationToken cancellationToken)
        => ToDto(await genres.InsertAsync(new Genre(CleanName(name)), cancellationToken));

    public async Task<GenreDto> UpdateGenreAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        var genre = await genres.FindAsync(g => g.Id == id, cancellationToken) ?? throw Missing("Genre");
        genre.Rename(CleanName(name));
        await genres.SaveAsync(cancellationToken);
        return ToDto(genre);
    }

    public async Task DeleteGenreAsync(Guid id, CancellationToken cancellationToken)
    {
        var genre = await genres.FindAsync(g => g.Id == id, cancellationToken) ?? throw Missing("Genre");
        if (await movies.AnyAsync(m => m.MovieGenres.Any(link => link.GenreId == id), cancellationToken)
            || await preferences.AnyAsync(p => p.GenreId == id, cancellationToken))
            throw new OperationException(FailureKind.Conflict, "Remove movie associations and preferences before deleting this genre.");
        await genres.DeleteAsync(genre, cancellationToken);
    }

    public async Task<List<MoodDto>> ListMoodsAsync(int skip, int take, CancellationToken cancellationToken)
        => (await moods.PageAsync(ValidateSkip(skip), ValidateTake(take), cancellationToken))
            .Select(ToDto).ToList();

    public async Task<MoodDto> GetMoodAsync(Guid id, CancellationToken cancellationToken)
        => ToDto(await RequireMood(id, cancellationToken));

    public async Task<MoodDto> CreateMoodAsync(string name, string description, CancellationToken cancellationToken)
        => ToDto(await moods.InsertAsync(new Mood(CleanName(name), CleanDescription(description)), cancellationToken));

    public async Task<MoodDto> UpdateMoodAsync(Guid id, string name, string description, CancellationToken cancellationToken)
    {
        var mood = await moods.FindAsync(m => m.Id == id, cancellationToken) ?? throw Missing("Mood");
        mood.Update(CleanName(name), CleanDescription(description));
        await moods.SaveAsync(cancellationToken);
        return ToDto(mood);
    }

    public async Task DeleteMoodAsync(Guid id, CancellationToken cancellationToken)
    {
        var mood = await moods.FindAsync(m => m.Id == id, cancellationToken) ?? throw Missing("Mood");
        if (await preferences.AnyAsync(p => p.MoodId == id, cancellationToken))
            throw new OperationException(FailureKind.Conflict, "Remove preferences before deleting this mood.");
        await moods.DeleteAsync(mood, cancellationToken);
    }

    public async Task<List<MovieDto>> ListMoviesAsync(int skip, int take, CancellationToken cancellationToken)
        => (await movies.PageAsync(ValidateSkip(skip), ValidateTake(take), cancellationToken))
            .Select(movie => movie.ToDto()).ToList();

    public async Task<MovieDto> GetMovieAsync(Guid id, CancellationToken cancellationToken)
        => (await movies.GetAsync(m => m.Id == id, cancellationToken) ?? throw Missing("Movie")).ToDto();

    public async Task<MovieDto> CreateMovieAsync(MovieInputDto input, CancellationToken cancellationToken)
    {
        var movie = new Movie(input.Title, input.Overview, input.RuntimeMinutes, input.ReleaseDate);
        await ApplyMovieAsync(movie, input, cancellationToken);
        await movies.InsertAsync(movie, cancellationToken);
        return await GetMovieAsync(movie.Id, cancellationToken);
    }

    public async Task<MovieDto> UpdateMovieAsync(Guid id, MovieInputDto input, CancellationToken cancellationToken)
    {
        var movie = await movies.FindAsync(m => m.Id == id, cancellationToken) ?? throw Missing("Movie");
        await ApplyMovieAsync(movie, input, cancellationToken);
        await movies.SaveAsync(cancellationToken);
        return await GetMovieAsync(movie.Id, cancellationToken);
    }

    public async Task DeleteMovieAsync(Guid id, CancellationToken cancellationToken)
    {
        var movie = await movies.FindAsync(m => m.Id == id, cancellationToken) ?? throw Missing("Movie");
        if (await watchlist.AnyAsync(e => e.MovieId == id, cancellationToken))
            throw new OperationException(FailureKind.Conflict, "Remove watchlist entries before deleting this movie.");
        await movies.DeleteAsync(movie, cancellationToken);
    }

    private async Task ApplyMovieAsync(Movie movie, MovieInputDto input, CancellationToken cancellationToken)
    {
        if (input.RuntimeMinutes is <= 0 || input.Overview?.Length > 4000)
            throw new OperationException(FailureKind.Validation, "Runtime must be positive and overview at most 4000 characters.");
        var ids = input.GenreIds?.Distinct().ToArray() ?? [];
        if (input.GenreIds is null || ids.Length != input.GenreIds.Count)
            throw new OperationException(FailureKind.Validation, "Genre IDs must be distinct.");
        var selected = await genres.ListAsync(g => ids.Contains(g.Id), cancellationToken);
        if (selected.Count != ids.Length)
            throw new OperationException(FailureKind.Validation, "Unknown genre ID.");
        movie.UpdateMetadata(CleanName(input.Title, 200), input.Overview?.Trim() ?? "",
            input.RuntimeMinutes, input.ReleaseDate);
        movie.ReplaceGenres(ids);
    }

    private async Task<Genre> RequireGenre(Guid id, CancellationToken cancellationToken)
        => await genres.GetAsync(g => g.Id == id, cancellationToken) ?? throw Missing("Genre");

    private async Task<Mood> RequireMood(Guid id, CancellationToken cancellationToken)
        => await moods.GetAsync(m => m.Id == id, cancellationToken) ?? throw Missing("Mood");

    private static GenreDto ToDto(Genre genre) => new(genre.Id, genre.Name, genre.TmdbId);
    private static MoodDto ToDto(Mood mood) => new(mood.Id, mood.Name, mood.Description);
    private static OperationException Missing(string type) => new(FailureKind.NotFound, $"{type} was not found.");
    private static string CleanName(string name, int maxLength = 100)
        => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= maxLength
            ? name.Trim() : throw new OperationException(FailureKind.Validation, $"Name must contain 1 to {maxLength} characters.");
    private static string CleanDescription(string description)
        => (description?.Trim() ?? "") is { Length: <= 1000 } value ? value
            : throw new OperationException(FailureKind.Validation, "Description must be at most 1000 characters.");
    private static int ValidateSkip(int skip) => skip >= 0 ? skip : throw new OperationException(FailureKind.Validation, "Skip must be nonnegative.");
    private static int ValidateTake(int take) => take is >= 1 and <= 100 ? take : throw new OperationException(FailureKind.Validation, "Take must be 1 to 100.");
}
