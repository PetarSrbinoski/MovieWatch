# MovieWatch

Personal and group movie recommendations based on mood and available time. Built with .NET 10 and SQLite.

## Run

Requires the .NET 10 SDK. From this directory:

```sh
dotnet user-secrets set 'Jwt:SigningKey' "$(openssl rand -base64 32)" --project MovieWatch.Web
dotnet run --project MovieWatch.Web
```

Open [MovieWatch](http://localhost:5080) or [Swagger](http://localhost:5080/swagger). The database is created automatically.

To populate the catalogue, set `Administrator:Enabled`, `Administrator:Email`, `Administrator:Password`, `Administrator:DisplayName`, and `Tmdb:ReadAccessToken` using user secrets. Sign in as administrator and use **Manage** to sync genres and import movies.

## Test

```sh
dotnet test MovieWatch.sln
```

![TMDB](MovieWatch.Web/wwwroot/tmdb-logo.svg)

This product uses the TMDB API but is not endorsed or certified by TMDB.
