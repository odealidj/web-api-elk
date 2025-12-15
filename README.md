# web-api-elk

Minimal .NET 8 Web API demo with:
- Auth endpoints (register & login) backed by SQL Server
- Password hashing using ASP.NET Core `IPasswordHasher<T>` with strong password policy
- Structured logging with Serilog (Console, File, HTTP to Logstash)
- CorrelationId middleware for tracing requests end-to-end
- ELK stack (Elasticsearch + Logstash + Kibana) via Docker Compose, with per-module indices

## Features

- `POST /api/auth/register`
  - Request body: `{ "username": "string", "password": "string", "email": "string?" }`
  - Password policy:
    - Minimum 8 characters
    - At least 1 uppercase letter
    - At least 1 lowercase letter
    - At least 1 digit
    - At least 1 symbol (non letter/digit)
  - Returns `SimpleResponse` with `Success`, `Message`, and `CorrelationId`.

- `POST /api/auth/login`
  - Request body: `{ "username": "string", "password": "string" }`
  - Returns `SimpleResponse` on success; `401 Unauthorized` on failure.

- Correlation ID
  - Optional request header: `X-Correlation-Id`
  - If not provided, server generates a new GUID.
  - Echoed back in response header and body (`SimpleResponse.CorrelationId`).
  - Included in all Serilog logs, so you can trace a single request in Kibana.

## Prerequisites

- .NET 8 SDK
- SQL Server instance (local or remote, **not** in Docker)
- Docker Desktop (for Elasticsearch, Logstash, Kibana)

## SQL Server & EF Core Setup

1. Update connection string in `src/web-api-elk/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=WebApiElk;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Adjust `Server`, `Database`, and authentication for your SQL Server.

2. Apply EF Core migrations (from `src/web-api-elk`):

```powershell
cd .\src\web-api-elk
# first time only
# dotnet tool install --global dotnet-ef
# add a migration once (if not created yet)
# dotnet ef migrations add InitialAuthSchema
# apply migrations
# dotnet ef database update
```

The schema creates a `Users` table with columns:
- `Id` (GUID, PK)
- `Username` (unique)
- `Email` (nullable)
- `PasswordHash`
- `CreatedAt`

## Logging with Serilog

Serilog is configured via `src/web-api-elk/appsettings.json`:

- Sinks:
  - Console
  - File: `Logs/log-.txt`
    - Rolling interval: 1 day
    - File size limit: **10MB**
    - Up to **7 files** kept per series
  - HTTP: sends JSON logs to Logstash at `http://localhost:5000`
- Global properties:
  - `Application = "web-api-elk"`
  - `Module = "auth"`

Auth operations log structured data:
- Username
- UserId (when available)
- CorrelationId
- Result (success/failure)

## ELK Stack (Elasticsearch + Logstash + Kibana)

The ELK stack runs in Docker (see `docker-compose.yml` and `logstash/pipeline/logstash.conf`).

### Logstash pipeline

`logstash/pipeline/logstash.conf`:

- Input: HTTP on port **5000**, JSON codec
- Filter: ensure field `module = "auth"` exists
- Output: Elasticsearch index pattern:
  - `webapi-elk-%{[module]}-logs-%{+YYYY.MM.dd}`
  - For this auth module: `webapi-elk-auth-logs-YYYY.MM.DD`

### Elasticsearch users (conceptual)

You should configure Elasticsearch security and create dedicated users:

- `log-writer`
  - Role: can **write** to indices `webapi-elk-*-logs-*`
  - Used by Logstash output to send logs
- `kibana-user`
  - Role: can **read** from `webapi-elk-*-logs-*` and access Kibana UI

In `logstash.conf`, Logstash uses `log-writer` credentials to write logs. Kibana uses `kibana-user` to read logs and build dashboards.

### Running ELK with Docker Compose

From the solution root (`D:\my-git-project\netcore\web-api-elk`):

```powershell
# start elasticsearch, logstash, and kibana
docker compose up -d elasticsearch logstash kibana

# check containers
docker ps
```

Access Kibana at:

```text
http://localhost:5601
```

Then create an index pattern (e.g. `webapi-elk-auth-logs-*`) and use `@timestamp` as the time field.

## Running the API

From `src/web-api-elk`:

```powershell
cd .\src\web-api-elk
dotnet restore
dotnet build
dotnet run
```

Check Swagger UI (port may differ based on your `launchSettings.json`):

```text
https://localhost:5001/swagger
``` 

## Example Requests

### Register

```http
POST /api/auth/register HTTP/1.1
Host: localhost:5001
Content-Type: application/json
X-Correlation-Id: 11111111-1111-1111-1111-111111111111

{
  "username": "testuser",
  "password": "Passw0rd!",
  "email": "test@example.com"
}
```

### Login

```http
POST /api/auth/login HTTP/1.1
Host: localhost:5001
Content-Type: application/json
X-Correlation-Id: 22222222-2222-2222-2222-222222222222

{
  "username": "testuser",
  "password": "Passw0rd!"
}
```

Responses include `CorrelationId` in both header and body and are logged to Serilog and ELK.
