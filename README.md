# Workout Companion Server

Self-hosted companion service for the Workout Companion Android app. Android remains authoritative; this server accepts immutable finalized-workout snapshots and provides a private, read-only browser view.

This implementation includes:

- SQLite persistence and automatic EF Core migrations
- bearer-token API authentication
- browser login using the same token and an HttpOnly session cookie
- `GET /health` and authenticated `GET /api/v1/info`
- idempotent `PUT /api/v1/workouts/{syncId}` ingestion
- bounded `POST /api/v1/workouts/batch` ingestion with per-workout results
- authenticated Dashboard, History, Progress, and Analytics pages
- server-side History filters and pagination with workout/set detail
- progression charts, PRs, volume, and estimated 1RM tracking
- yearly workout heatmaps and program/training-day summaries
- filtered CSV and JSON history exports
- Docker, Compose, automated tests, and CI

## Run with Docker Compose

1. Copy `.env.example` to `.env`.
2. Replace `WORKOUT_API_TOKEN` with a long random value (for example, `openssl rand -base64 48`).
3. Start the service:

   ```sh
   docker compose up --build -d
   ```

4. Verify `http://localhost:8080/health`, then open `http://localhost:8080` and log in with the token.

Workout data and browser-session protection keys are stored in the Compose-managed `workout-data` volume. A bind mount may be substituted if you prefer host-visible files; ensure container user `1654` can write that directory.

If `WORKOUT_API_TOKEN` is omitted, the server generates a token on first start, prints its plaintext value exactly once, and stores only its SHA-256 hash in `/data/token.sha256`. Capture that first-start log securely; the plaintext cannot be recovered later. Avoid leaving an empty `WORKOUT_API_TOKEN=` entry in `.env` unless you intentionally want first-start generation.

## TLS and reverse proxy

The application container intentionally listens over plain HTTP on port `8080`. For Android access outside a trusted local network, put a reverse proxy such as Caddy, nginx, or Traefik in front of it and expose HTTPS. Never send the API token over cleartext HTTP across an untrusted network.

A minimal Caddy configuration for a normal public hostname looks like:

```caddyfile
workout.example.com {
    reverse_proxy workout-companion:8080
}
```

If Caddy runs in the same Compose project, it can reach `workout-companion:8080` over the internal Docker network. Publish port `443` from the proxy. Port `8080` may remain published as a separate convenience endpoint for trusted LAN administration or browser access, but it should not be exposed directly to the public Internet.

### Internal/private-IP hostnames

A hostname may resolve publicly to a private RFC1918 address such as `192.168.x.x` when the service is intended only for LAN or VPN clients. In that case a public certificate authority cannot complete HTTP-01 or TLS-ALPN-01 validation against the private address. Use an ACME DNS-01 challenge instead.

The exact DNS provider configuration belongs to the deployment, not to this repository. Caddy DNS-01 support normally requires a build containing the appropriate DNS provider module.

Some DNS providers do not publish API changes immediately. If a provider batches or delays DNS updates, configure an ACME propagation delay/timeout long enough for the temporary `_acme-challenge` TXT record to become visible on the authoritative nameservers before validation starts.

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

### Workout identity and duplicates

`SyncId` is the stable identity of a workout on the server. Re-uploading a workout with the same `SyncId` is idempotent. Uploading the same real-world workout with a different `SyncId` creates a separate workout record.

The server deliberately does not try to merge workouts heuristically by timestamp, program name, duration, or exercise content, because two legitimate workouts may otherwise be merged incorrectly. If a client migration, reinstall, import, or build-variant change regenerates historical sync IDs, duplicate cleanup must be handled explicitly by the administrator.

## Web UI

Browser routes require login with the configured server token:

- `/` — synchronization dashboard and recent workouts
- `/history` — filtered, paginated workout history
- `/workouts/{syncId}` — workout, exercise, and set details
- `/progress` and `/progress/{trackSyncId}` — progression targets, charts, and PRs
- `/analytics` — yearly heatmap and program/training-day summaries
- `/export/csv` and `/export/json` — downloads using the same query filters as History

The web UI is read-only. Export routes use cookie authentication and do not change API v1.

## Maintenance and SQLite

The default SQLite database is stored at:

```text
/data/workout-companion.db
```

inside the persistent `workout-data` volume. Do not install troubleshooting tools into the production application container just to inspect the database. A disposable helper container can mount the same volume instead.

For example:

```sh
docker run --rm -it \
  --volumes-from workout-companion \
  alpine:3.20 sh
```

Then, inside the temporary container:

```sh
apk add --no-cache sqlite
sqlite3 /data/workout-companion.db
```

The helper container is removed automatically when you exit because it was started with `--rm`. Changes made to `/data`, however, affect the real persistent volume immediately.

Before making manual database changes, back up the data volume. The safest simple option is to stop the application and archive the entire `/data` directory:

```sh
docker compose stop workout-companion

docker run --rm \
  --volumes-from workout-companion \
  -v "$PWD:/backup" \
  alpine:3.20 \
  sh -c 'tar czf /backup/workout-data-backup.tar.gz -C /data .'

docker compose start workout-companion
```

The full volume contains more than the SQLite database: it may also contain the generated token hash and ASP.NET Core data-protection keys. Backing up the whole volume preserves the complete server state.

SQLite foreign-key enforcement is important when performing manual deletes because workout exercises and sets use cascading relationships. In an interactive SQLite session, verify it before destructive maintenance:

```sql
PRAGMA foreign_keys = ON;
PRAGMA foreign_keys;
```

The second statement should return `1`.

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

Stop the container, back up the persistent data volume, and then deploy the new image. Compatible migrations are applied automatically at startup; startup fails if migration fails. The current API version and accepted payload schema version are both `1`.

## Privacy and MVP limitations

The service contains no telemetry, third-party analytics, external identity provider, or cloud dependency. It is single-user and read-only from the web. MVP does not synchronize deletions, edit workouts, merge progression tracks, or support bidirectional sync.
