using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MovieWatch.Domain.Models;

namespace MovieWatch.Repository;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Viewer> Viewers => Set<Viewer>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Mood> Moods => Set<Mood>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();
    public DbSet<GenrePreference> GenrePreferences => Set<GenrePreference>();
    public DbSet<WatchlistEntry> WatchlistEntries => Set<WatchlistEntry>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<IdentityUser>().HasIndex(a => a.NormalizedEmail).IsUnique();
        builder.Entity<Viewer>(viewer =>
        {
            viewer.Property(v => v.DisplayName).HasMaxLength(100).IsRequired();
            viewer.Property(v => v.AccountId).IsRequired();
            viewer.HasIndex(v => v.AccountId).IsUnique();
            viewer.HasOne<IdentityUser>().WithOne().HasForeignKey<Viewer>(v => v.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Genre>(genre =>
        {
            genre.Property(g => g.Name).HasMaxLength(100).IsRequired();
            genre.HasIndex(g => g.Name).IsUnique().HasFilter("TmdbId IS NULL");
            genre.HasIndex(g => g.TmdbId).IsUnique();
        });
        builder.Entity<Mood>(mood =>
        {
            mood.Property(m => m.Name).HasMaxLength(100).IsRequired();
            mood.Property(m => m.Description).HasMaxLength(1000);
            mood.HasIndex(m => m.Name).IsUnique();
        });
        builder.Entity<Movie>(movie =>
        {
            movie.Property(m => m.Title).HasMaxLength(200).IsRequired();
            movie.Property(m => m.Overview).HasMaxLength(4000);
            movie.HasIndex(m => m.TmdbId).IsUnique();
            movie.Navigation(m => m.MovieGenres).AutoInclude();
        });
        builder.Entity<MovieGenre>(link =>
        {
            link.HasKey(mg => new { mg.MovieId, mg.GenreId });
            link.HasOne(mg => mg.Movie).WithMany(m => m.MovieGenres).HasForeignKey(mg => mg.MovieId).OnDelete(DeleteBehavior.Cascade);
            link.HasOne(mg => mg.Genre).WithMany().HasForeignKey(mg => mg.GenreId).OnDelete(DeleteBehavior.Restrict);
            link.Navigation(mg => mg.Genre).AutoInclude();
        });
        builder.Entity<GenrePreference>(preference =>
        {
            preference.HasIndex(p => new { p.ViewerId, p.GenreId, p.MoodId }).IsUnique();
            preference.ToTable(t => t.HasCheckConstraint("CK_Preference_Weight", "Weight BETWEEN -2 AND 2"));
            preference.HasOne<Viewer>().WithMany().HasForeignKey(p => p.ViewerId).OnDelete(DeleteBehavior.Cascade);
            preference.HasOne<Genre>().WithMany().HasForeignKey(p => p.GenreId).OnDelete(DeleteBehavior.Restrict);
            preference.HasOne<Mood>().WithMany().HasForeignKey(p => p.MoodId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<WatchlistEntry>(entry =>
        {
            entry.HasIndex(e => new { e.ViewerId, e.MovieId }).IsUnique();
            entry.Property(e => e.Note).HasMaxLength(1000);
            entry.HasOne<Viewer>().WithMany().HasForeignKey(e => e.ViewerId).OnDelete(DeleteBehavior.Cascade);
            entry.HasOne<Movie>().WithMany().HasForeignKey(e => e.MovieId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Group>(group =>
        {
            group.Property(g => g.Name).HasMaxLength(100).IsRequired();
            group.Property(g => g.Description).HasMaxLength(1000);
            group.HasOne<Viewer>().WithMany().HasForeignKey(g => g.OwnerViewerId).OnDelete(DeleteBehavior.Restrict);
            group.Navigation(g => g.Memberships).AutoInclude();
        });
        builder.Entity<GroupMembership>(member =>
        {
            member.HasIndex(m => new { m.GroupId, m.ViewerId }).IsUnique();
            member.HasOne<Group>().WithMany(g => g.Memberships).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
            member.HasOne<Viewer>().WithMany().HasForeignKey(m => m.ViewerId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ImportJob>(job =>
        {
            job.Property(j => j.Error).HasMaxLength(1000);
            job.HasIndex(j => new { j.Status, j.NextAttemptAt });
        });
    }
}
