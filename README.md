# OpenBrew

OpenBrew is an ASP.NET Core (.NET 8) Web API that serves a searchable, sortable, paginated catalog of open breweries. Brewery data is sourced from the public [Open Brewery DB](https://www.openbrewerydb.org/) API, periodically refreshed into a local SQLite database, and served from an in-memory cache for fast reads. The API is secured with JWT bearer authentication and versioned via URL segments.

## Features

- **Brewery search** — filter by city and free-text search, sort by name, city, or distance from a given coordinate, with pagination.
- **Autocomplete** — prefix/substring suggestions for brewery names.
- **Background refresh** — a hosted service periodically re-fetches data from Open Brewery DB, persists it to SQLite, and updates the in-memory cache; a seed file is used to populate the database on first run.
- **JWT authentication** — a login endpoint issues bearer tokens; brewery endpoints require a valid token.
- **API versioning** — routes are versioned (`/api/v1/...`) using `Asp.Versioning`.
- **Swagger / OpenAPI** — interactive docs available in Development, including a "Bearer" auth button for testing secured endpoints.
- **Global exception handling** — unhandled exceptions are logged and converted to safe `ProblemDetails` responses.
- **Explicit CORS policy** — cross-origin requests are denied by default and can be enabled for configured origins.
- **Production transport security** — HSTS is enabled in the Production environment for 180 days, including subdomains.
- **Structured logging** — `ILogger` is used throughout controllers, services, and middleware.

## Project Structure

```
OpenBrew.sln
OpenBrew.Api/                   # API project
  Clients/                      # HTTP client for the upstream Open Brewery DB API
  Contracts/                    # Request/response DTOs
  Controllers/                  # AuthController, BreweriesController
  Data/                         # EF Core DbContext, entities, DB initializer/seed data
  Middleware/                   # Global exception handler
  Models/                       # Domain model (Brewery)
  Options/                      # Strongly typed configuration (Jwt, DemoUser, OpenBrewery)
  Services/                     # Business logic, caching, refresh, JWT token issuing
  Program.cs                    # App composition root
OpenBrew.Api.Tests/              # xUnit unit + integration tests
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- No external database server required — brewery data is stored in a local SQLite file (`openbrewery.db`), created automatically on startup.

## Getting Started

```powershell
# Restore and build
dotnet build OpenBrew.sln

# Run the API (Development profile, launches Swagger UI)
dotnet run --project OpenBrew.Api
```

By default the API listens on `http://localhost:5123` (see [OpenBrew.Api/Properties/launchSettings.json](OpenBrew.Api/Properties/launchSettings.json)). On first run it creates the SQLite database and seeds it from [OpenBrew.Api/Data/Seed/breweries.json](OpenBrew.Api/Data/Seed/breweries.json); a background service then periodically refreshes it from the live Open Brewery DB API.

Swagger UI is available at `/swagger` in the Development environment.

## Authentication

All `breweries` endpoints require a JWT bearer token. Obtain one via the login endpoint using the demo credentials configured in `appsettings.Development.json`:

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "ChangeMe123!"
}
```

Response:

```json
{
  "accessToken": "<jwt>",
  "expiresAtUtc": "2026-10-07T13:00:00Z"
}
```

Send the token on subsequent requests:

```http
GET /api/v1/breweries
Authorization: Bearer <jwt>
```

In Swagger UI, click **Authorize** and paste the token (prefixed with `Bearer `).

> **Note:** This project ships with a single demo user for simplicity — there is no user database. `Jwt:SigningKey` and `DemoUser:Password` are intentionally left blank in `appsettings.json` and must be supplied per environment (user secrets, environment variables, or a vault). Development and test values are provided in `appsettings.Development.json` / `appsettings.Testing.json` for local use only — never reuse those values in production.

## Configuration

Settings are bound from `appsettings.json` (and environment-specific overrides) via the `IOptions<T>` pattern with validation on startup.

| Section | Key | Description |
|---|---|---|
| `ConnectionStrings` | `OpenBrewery` | SQLite connection string (defaults to `Data Source=openbrewery.db`). |
| `OpenBrewery` | `BaseUrl` | Base URL of the upstream Open Brewery DB API. |
| `OpenBrewery` | `CacheDurationMinutes` | How long data is cached in memory and how often the background refresh runs. |
| `OpenBrewery` | `PerPage` | Page size used when paging through the upstream API (1–200). |
| `OpenBrewery` | `MaxPagesToFetch` | Maximum number of upstream pages fetched per refresh. |
| `Jwt` | `Issuer` / `Audience` | Token issuer/audience validated on incoming requests. |
| `Jwt` | `SigningKey` | Symmetric key (HMAC-SHA256) used to sign/validate tokens; must be at least 32 characters. |
| `Jwt` | `ExpiryMinutes` | Token lifetime in minutes. |
| `DemoUser` | `Username` / `Password` | Credentials accepted by `/api/v1/auth/login`. |
| `Cors` | `AllowedOrigins` | Array of exact origins allowed for cross-origin requests; empty by default (fail closed). |

For example, configure `Cors:AllowedOrigins` with `https://app.example.com` for a deployed frontend. With environment variables, use `Cors__AllowedOrigins__0` for the first origin, `Cors__AllowedOrigins__1` for the second, and so on. Do not use `*` when credentials are required.

## API Reference

All routes are prefixed with `/api/v1`.

### `POST /auth/login`
Authenticates the demo user and issues a JWT. No auth required.

### `GET /breweries` *(requires auth)*
Query parameters:

| Parameter | Type | Notes |
|---|---|---|
| `search` | string | Case-insensitive match against name and city. |
| `city` | string | Case-insensitive partial city filter. |
| `sortBy` | string | `name` \| `city` \| `distance` (default `name`). |
| `sortDirection` | string | `asc` \| `desc` (default `asc`). |
| `latitude`, `longitude` | double | Required when `sortBy=distance`. |
| `page` | int | 1-based page number (default 1). |
| `pageSize` | int | 1–200 (default 50). |

Returns a list of `{ name, city, phone, distanceKm }`.

### `GET /breweries/autocomplete` *(requires auth)*
Query parameters:

| Parameter | Type | Notes |
|---|---|---|
| `term` | string | Required, minimum length 1. |
| `limit` | int | 1–50 (default 10). |

Returns an ordered list of matching brewery names.

See [OpenBrew.Api/OpenBrew.Api.http](OpenBrew.Api/OpenBrew.Api.http) for ready-to-run sample requests (including login + token reuse) if using the VS Code/Visual Studio HTTP client.

## Running Tests

```powershell
dotnet test OpenBrew.sln
```

The test suite ([OpenBrew.Api.Tests](OpenBrew.Api.Tests)) includes:
- Unit tests for services (`BreweryService`, `BreweryRefreshService`, `SqliteBreweryStore`) and the Open Brewery DB client.
- Integration tests ([BreweriesApiTests.cs](OpenBrew.Api.Tests/Integration/BreweriesApiTests.cs)) that boot the full API in-memory via `WebApplicationFactory`, exercising authentication, filtering, sorting, pagination, and error handling.

## Architecture Notes

- **Data flow:** `BreweryRefreshBackgroundService` → `BreweryRefreshService` fetches from `OpenBreweryDbClient`, persists to SQLite (`OpenBreweryDbContext`), and updates `BreweryMemoryCache`. Reads go through `SqliteBreweryStore`, which prefers the memory cache and falls back to SQLite. `BreweryService` applies filtering, sorting, and pagination on top of the cached data.
- **Error handling:** `GlobalExceptionHandler` (registered via `AddExceptionHandler`) converts unhandled exceptions into `ProblemDetails` and logs them; validation errors are handled automatically via model binding/data annotations.
- **Security:** JWT bearer authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`) validates issuer, audience, signing key, and lifetime on every request to protected endpoints.
