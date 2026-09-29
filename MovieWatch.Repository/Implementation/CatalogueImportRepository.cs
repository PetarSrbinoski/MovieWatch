using Microsoft.EntityFrameworkCore;
using MovieWatch.Domain.Dto;
using MovieWatch.Domain.Models;
using MovieWatch.Repository.Interface;

namespace MovieWatch.Repository.Implementation;

public sealed class CatalogueImportRepository(ApplicationDbContext context) : ICatalogueImportRepository
{
    public async Task UpsertAsync(ImportedMovieDto imported, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var genreIds = new List<Guid>();
        foreach (var external in imported.Genres)
        {
            var genre = await context.Genres.SingleOrDefaultAsync(g => g.TmdbId == external.TmdbId, cancellationToken)
                ?? await context.Genres.SingleOrDefaultAsync(g => g.TmdbId == null && g.Name == external.Name, cancellationToken);
            if (genre is null)
            {
                genre = new Genre(external.Name, external.TmdbId);
                context.Genres.Add(genre);
            }
            else
            {
                genre.Refresh(external.TmdbId, external.Name);
            }
            genreIds.Add(genre.Id);
        }
        await context.SaveChangesAsync(cancellationToken);
        var movie = await context.Movies.SingleOrDefaultAsync(m => m.TmdbId == imported.TmdbId, cancellationToken);
        if (movie is null)
        {
            movie = new Movie(imported.Title, imported.Overview, imported.RuntimeMinutes, imported.ReleaseDate);
            context.Movies.Add(movie);
        }
        movie.Refresh(imported.TmdbId, imported.Title, imported.Overview, imported.RuntimeMinutes,
            imported.ReleaseDate, imported.VoteCount, now);
        var desired = genreIds.ToHashSet();
        foreach (var old in movie.MovieGenres.Where(link => !desired.Contains(link.GenreId)))
            context.MovieGenres.Remove(old);
        foreach (var id in desired.Where(id => movie.MovieGenres.All(link => link.GenreId != id)))
            context.MovieGenres.Add(new MovieGenre(movie.Id, id));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
