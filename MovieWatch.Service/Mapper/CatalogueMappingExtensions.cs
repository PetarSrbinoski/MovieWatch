using MovieWatch.Domain.Dto;
using MovieWatch.Domain.Models;

namespace MovieWatch.Service.Mapper;

public static class CatalogueMappingExtensions
{
    public static MovieDto ToDto(this Movie movie) => new(movie.Id, movie.Title, movie.Overview,
        movie.RuntimeMinutes, movie.ReleaseDate, movie.TmdbId, movie.VoteCount, movie.LastImportedAt,
        movie.MovieGenres.OrderBy(link => link.Genre.Name)
            .Select(link => new GenreDto(link.GenreId, link.Genre.Name, link.Genre.TmdbId)).ToArray());
}
