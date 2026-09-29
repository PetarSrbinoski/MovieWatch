using MovieWatch.Domain.Dto;

namespace MovieWatch.Service.Interface;

public interface ICatalogueService
{
    Task<List<GenreDto>> ListGenresAsync(int skip, int take, CancellationToken cancellationToken);
    Task<GenreDto> GetGenreAsync(Guid id, CancellationToken cancellationToken);
    Task<GenreDto> CreateGenreAsync(string name, CancellationToken cancellationToken);
    Task<GenreDto> UpdateGenreAsync(Guid id, string name, CancellationToken cancellationToken);
    Task DeleteGenreAsync(Guid id, CancellationToken cancellationToken);
    IReadOnlyList<MoodPresetDto> ListMoodPresets();
    Task<List<MoodDto>> ListMoodsAsync(int skip, int take, CancellationToken cancellationToken);
    Task<MoodDto> GetMoodAsync(Guid id, CancellationToken cancellationToken);
    Task<MoodDto> CreateMoodAsync(string name, string description, CancellationToken cancellationToken, string? presetKey = null);
    Task<MoodDto> UpdateMoodAsync(Guid id, string name, string description, CancellationToken cancellationToken, string? presetKey = null);
    Task DeleteMoodAsync(Guid id, CancellationToken cancellationToken);
    Task<List<MovieDto>> ListMoviesAsync(int skip, int take, CancellationToken cancellationToken);
    Task<MovieDto> GetMovieAsync(Guid id, CancellationToken cancellationToken);
    Task<MovieDto> CreateMovieAsync(MovieInputDto input, CancellationToken cancellationToken);
    Task<MovieDto> UpdateMovieAsync(Guid id, MovieInputDto input, CancellationToken cancellationToken);
    Task DeleteMovieAsync(Guid id, CancellationToken cancellationToken);
}
