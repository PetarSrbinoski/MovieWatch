using MovieWatch.Domain.Dto;
using MovieWatch.Service.Interface;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Mapper;

public sealed class CatalogueMapper(ICatalogueService catalogue)
{
    public Task<List<GenreDto>> ListGenresAsync(int skip, int take, CancellationToken cancellationToken)
        => catalogue.ListGenresAsync(skip, take, cancellationToken);
    public Task<GenreDto> GetGenreAsync(Guid id, CancellationToken cancellationToken)
        => catalogue.GetGenreAsync(id, cancellationToken);
    public Task<GenreDto> CreateGenreAsync(GenreRequest request, CancellationToken cancellationToken)
        => catalogue.CreateGenreAsync(request.Name, cancellationToken);
    public Task<GenreDto> UpdateGenreAsync(Guid id, GenreRequest request, CancellationToken cancellationToken)
        => catalogue.UpdateGenreAsync(id, request.Name, cancellationToken);
    public Task DeleteGenreAsync(Guid id, CancellationToken cancellationToken)
        => catalogue.DeleteGenreAsync(id, cancellationToken);
    public Task<List<MoodDto>> ListMoodsAsync(int skip, int take, CancellationToken cancellationToken)
        => catalogue.ListMoodsAsync(skip, take, cancellationToken);
    public Task<MoodDto> GetMoodAsync(Guid id, CancellationToken cancellationToken)
        => catalogue.GetMoodAsync(id, cancellationToken);
    public Task<MoodDto> CreateMoodAsync(MoodRequest request, CancellationToken cancellationToken)
        => catalogue.CreateMoodAsync(request.Name, request.Description, cancellationToken);
    public Task<MoodDto> UpdateMoodAsync(Guid id, MoodRequest request, CancellationToken cancellationToken)
        => catalogue.UpdateMoodAsync(id, request.Name, request.Description, cancellationToken);
    public Task DeleteMoodAsync(Guid id, CancellationToken cancellationToken)
        => catalogue.DeleteMoodAsync(id, cancellationToken);
    public Task<List<MovieDto>> ListMoviesAsync(int skip, int take, CancellationToken cancellationToken)
        => catalogue.ListMoviesAsync(skip, take, cancellationToken);
    public Task<MovieDto> GetMovieAsync(Guid id, CancellationToken cancellationToken)
        => catalogue.GetMovieAsync(id, cancellationToken);
    public Task<MovieDto> CreateMovieAsync(MovieRequest request, CancellationToken cancellationToken)
        => catalogue.CreateMovieAsync(request.ToDto(), cancellationToken);
    public Task<MovieDto> UpdateMovieAsync(Guid id, MovieRequest request, CancellationToken cancellationToken)
        => catalogue.UpdateMovieAsync(id, request.ToDto(), cancellationToken);
    public Task DeleteMovieAsync(Guid id, CancellationToken cancellationToken)
        => catalogue.DeleteMovieAsync(id, cancellationToken);
}

public static class CatalogueMappingExtensions
{
    public static GenreResponse ToResponse(this GenreDto dto) => new(dto.Id, dto.Name, dto.TmdbId);
    public static MoodResponse ToResponse(this MoodDto dto) => new(dto.Id, dto.Name, dto.Description);
    public static MovieResponse ToResponse(this MovieDto dto) => new(dto.Id, dto.Title, dto.Overview,
        dto.RuntimeMinutes, dto.ReleaseDate, dto.TmdbId, dto.VoteCount, dto.LastImportedAt,
        dto.Genres.Select(g => g.ToResponse()).ToArray());
    public static MovieInputDto ToDto(this MovieRequest request) => new(request.Title, request.Overview,
        request.RuntimeMinutes, request.ReleaseDate, request.GenreIds);
}
