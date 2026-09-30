# MovieWatch

MovieWatch is a .NET 10 API for explainable personal and group movie recommendations. It includes authenticated profiles, a movie catalogue, mood-specific genre preferences, watchlists, groups, persisted TMDB imports, and Excel downloads.

See [IMPLEMENTATION.md](IMPLEMENTATION.md) for what was built, how it works, every route, setup, and verification. The [specification](docs/spec.md), [design](docs/design.md), and [domain terms](CONTEXT.md) are included for the course assessment.

## Quick start

From this directory, with the .NET 10 SDK installed:

```sh
dotnet restore
dotnet tool restore
dotnet user-secrets set 'Jwt:SigningKey' "$(openssl rand -base64 32)" --project MovieWatch.Web
dotnet run --project MovieWatch.Web
```

Open `http://localhost:5080/swagger`. SQLite migrations run at startup. The database path defaults to `moviewatch.db` in the process working directory. [MovieWatch.http](MovieWatch.Web/MovieWatch.http) contains a reproducible request sequence.

For administrator operations, configure `Administrator:Enabled`, `Administrator:Email`, `Administrator:Password`, and `Administrator:DisplayName` with user secrets before starting the app. For live imports, configure `Tmdb:ReadAccessToken`. Keep these values out of Git.

## Checks

```sh
dotnet build MovieWatch.sln
dotnet test MovieWatch.sln
```

The tests cover real SQLite persistence, JWT and role checks, catalogue and personal-data workflows, group scoring and ownership, queue retries and restart recovery, Excel contents, and the course architecture.

## Attribution

![TMDB logo](MovieWatch.Web/wwwroot/tmdb-logo.svg)

This product uses the TMDB API but is not endorsed or certified by TMDB. [TMDB attribution guidance](https://developer.themoviedb.org/docs/faq).
