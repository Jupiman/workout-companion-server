# Workout Companion Server

Self-hosted companion service for the Workout Companion Android app. Android remains authoritative; this server accepts immutable finalized-workout snapshots and provides a private, read-only browser view.

This initial implementation includes:

- SQLite persistence and automatic EF Core migrations
- bearer-token API authentication
- browser login using the same token and an HttpOnly session cookie
- `GET /health` and authenticated `GET /api/v1/info`
- idempotent `PUT /api/v1/workouts/{syncId}` ingestion
- bounded `POST /api/v1/workouts/batch` ingestion with per-workout results
- a small authenticated dashboard showing persisted counts and recent workouts
- Docker, Compose, automated tests, and CI

Android sync and the complete History/Progress UI remain follow-up phases.

## Run with Docker Compose

1. Copy `.env.example` to `.env`.
2. Replace `WORKOUT_API_TOKEN` with a long random value (for example, `openssl rand -base64 48`).
3. Start the service:

   ```sh
   docker compose up --build -d
   ```

4. Verify `http://localhost:8080/health`, then open `http://localhost:8080` and log in with the token.

Workout data and browser-session protection keys are stored in the Compose-managed `workout-data` volume. Back up the whole volume while the container is stopped, or use SQLite's online backup facilities for the database. A bind mount may be substituted if you prefer host-visible files; ensure container user `1654` can write that directory.

If `WORKOUT_API_TOKEN` is omitted, the server generates a token on first start, prints its plaintext value exactly once, and stores only its SHA-256 hash in `/data/token.sha256`. Capture that first-start log securely; the plaintext cannot be recovered later. Avoid leaving an empty `WORKOUT_API_TOKEN=` entry in `.env` unless you intentionally want first-start generation.

## TLS and reverse proxy

The container intentionally listens over HTTP on port 8080. Put Caddy, nginx, or Traefik in front of it and expose HTTPS to Android. Never send the API token over cleartext HTTP outside local development.

When TLS is terminated by a trusted reverse proxy, forward the original scheme and host. Configure ASP.NET Core forwarded-header trust for your deployment before exposing it publicly; do not accept forwarded headers from arbitrary internet clients.

## API

All `/api/v1` requests require:

```http
Authorization: Bearer <token>
```

`PUT /api/v1/workouts/{syncId}` accepts one complete finalized workout. Re-uploading the same sync ID transactionally replaces its stored snapshot, so retries and restore catch-up cannot create duplicates.

`POST /api/v1/workouts/batch` accepts:

```json
{ "workouts": [ /* up to 50 workout payloads */ ] }
```

Each item is validated and committed independently. The response reports `CREATED`, `UPDATED`, or `REJECTED` per workout.

## Development

Requires the current .NET 10 LTS SDK:

```sh
dotnet restore WorkoutCompanionServer.sln
dotnet build WorkoutCompanionServer.sln
dotnet test WorkoutCompanionServer.sln
```

For local development, set `Workout__ApiToken` or `WORKOUT_API_TOKEN`. The default data directory is `./runtime-data`; override it with `WORKOUT_DATA_DIRECTORY`.

SQLite persists absolute timestamps as UTC Unix epoch milliseconds in `INTEGER` columns. The API continues to accept and return normal ISO-8601 timestamps with offsets.

## Upgrades and compatibility

Stop the container, back up `./data`, and then deploy the new image. Compatible migrations are applied automatically at startup; startup fails if migration fails. The current API version and accepted payload schema version are both `1`.

## Privacy and MVP limitations

The service contains no telemetry, third-party analytics, external identity provider, or cloud dependency. It is single-user and read-only from the web. MVP does not synchronize deletions, edit workouts, merge progression tracks, or support bidirectional sync.
