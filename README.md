# MovieWatch

MovieWatch is a .NET 10 API for explainable personal and group movie recommendations. It includes authenticated profiles, a movie catalogue, mood-specific genre preferences, watchlists, groups, persisted TMDB imports, and Excel downloads.

## Quick start

From this directory, with the .NET 10 SDK installed:

```sh
dotnet restore
dotnet tool restore
dotnet user-secrets set 'Jwt:SigningKey' "$(openssl rand -base64 32)" --project MovieWatch.Web
dotnet run --project MovieWatch.Web
```

Open `http://localhost:5080/` for the MovieWatch interface, or `http://localhost:5080/swagger` for the full API. SQLite migrations run at startup. The database path defaults to `moviewatch.db` in the process working directory.

In Rider, run the **MovieWatch.Web** project and open `http://localhost:5080/`. Opening `wwwroot/index.html` with **Open in Browser** uses Rider’s static preview server on port 63342; it can display the UI, but the API features require the running .NET app on port 5080.

For administrator operations, configure `Administrator:Enabled`, `Administrator:Email`, `Administrator:Password`, and `Administrator:DisplayName` with user secrets before starting the app. For live imports, configure `Tmdb:ReadAccessToken`. Keep these values out of Git.

## Interface

Discover is the starting point: choose who is watching, a mood, and a maximum runtime. The 90-minute, 2-hour, and 3-hour shortcuts update matches immediately; a custom minute limit is also available. **Fine-tune your picks** contains optional genres, watched movies, page size, and mood preferences. **Reset filters** clears those optional filters without changing the mood, runtime, or saved preferences.

Movie cards feature larger posters on desktop and compact poster layouts on phones, readable titles, short match reasons, and explicit watchlist states. Open a poster, title, or **Why it fits** for the full explanation and score contributions. **Export picks** downloads the displayed page as Excel.

Discover, Catalogue, and Watchlist stay visible on phones; **More** reveals Groups, Preferences, and administrator-only Manage. Profile contains account settings, sign-out, and the viewer ID used to join groups. Administrators retain movie, viewer, genre, mood, and TMDB import management.

Sign in or create an account to start. The app seeds eleven editable mood presets, but no sample movies, genres, or demo accounts. Administrators sync the full genre list from TMDB, optionally add custom moods, and import movies through Manage. A fresh database starts with an empty catalogue. The UI uses the existing API directly; no frontend build step or additional dependencies are required.

## TMDB setup

Create a TMDB account and obtain the **API Read Access Token** from [Settings → API](https://www.themoviedb.org/settings/api). This application uses Bearer authentication with that token, not the shorter API key.

```sh
dotnet user-secrets set "Tmdb:ReadAccessToken" "YOUR_READ_ACCESS_TOKEN" --project MovieWatch.Web
```

Run this in the same Windows or WSL environment as the app, then restart. As administrator, open **Manage → TMDB imports**, check the setup status, and queue one discovery page. The UI shows progress, imported/skipped counts, and failures. Live imports require the import worker (`Import:WorkerEnabled`, true by default). Use **Manage → Genres → Sync all genres from TMDB** to load every official movie genre independently of the imported movies. Repeat sync to update names while preserving IDs and preferences. Moods are editable under **Manage → Moods**, where administrators can choose a starting preset independently of the mood name. Movie details identify whether the movie came from TMDB or was added manually. Imports also save TMDB poster paths; movie cards load posters on demand and show a fallback when a poster is unavailable. Reimport existing movies to update their poster metadata. [TMDB authentication documentation](https://developer.themoviedb.org/docs/authentication-application).


Group, preference, and watchlist collection endpoints accept `skip=0` and `take=20` by default, with a maximum `take` of 100. Access filters are applied before pagination. The interface retrieves these collections in bounded pages.

## Checks

```sh
dotnet build MovieWatch.sln
dotnet test MovieWatch.sln
```

The tests cover real SQLite persistence, JWT and role checks, catalogue and personal-data workflows, group scoring and ownership, queue retries and restart recovery, Excel contents, and the course architecture.

When sharing this checkout between Windows Rider and Linux/WSL, build and restore files are separated under `bin/<OS>/` and `obj/<OS>/`. NuGet restore files contain absolute package paths, so sharing them across operating systems can cause Rider to report unresolved symbols such as `Database` even when a Linux build succeeds. After updating an existing checkout, run `dotnet restore MovieWatch.sln` in a **Windows** terminal in this directory, then reload the solution in Rider.

## Attribution

![TMDB logo](MovieWatch.Web/wwwroot/tmdb-logo.svg)

This product uses the TMDB API but is not endorsed or certified by TMDB. [TMDB attribution guidance](https://developer.themoviedb.org/docs/faq).


Discovery shows six results per page by default, with Previous/Next controls and a page count. Filter changes return to the first page. Presets provide genre weights immediately; personal preferences override them, including explicit Neutral. Use **Fine-tune your picks → Adjust this mood** to Favor, stay Neutral, Avoid (lower rank), or restore Preset for any genre. Filter changes refresh results automatically. **Show other options** includes neutral/negative alternatives after positive matches without relaxing your filters. The API uses `skip=0`, `limit=6` by default (maximum 50 per page), and `includeOtherOptions=false`; responses include `skip`, `limit`, and `totalCount`. Excel exports use the displayed page. Custom moods without a preset require personal preferences to produce positive matches.

Page headings and the wordmark use locally served Bebas Neue from Google Fonts; its SIL Open Font License is included in `wwwroot/fonts/BebasNeue-OFL.txt`. Movie titles and interface text use the system sans-serif stack for readability. Warm charcoal surfaces, ivory text, and amber accents give the interface a cinema-inspired identity.

The preset catalogue covers all 19 TMDB movie genres. In addition to laugh, suspense, adventure, scare and think, it includes **Pick a family film** (Family/Animation), **Share a love story** (Romance), **Follow a crime story** (Crime/Mystery/Thriller), **Step back in time** (History/War/Western), **Feel the music** (Music), and **Have a TV movie night** (TV Movie). The one-time legacy cleanup removes the original 20 mood names when they have no preset; later custom moods remain editable. Legacy preferences move to the closest replacement, with existing replacement preferences taking precedence on conflicts.
