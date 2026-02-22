# CountryGwpSolution

Self-hosted ASP.NET Core Web API (Kestrel) that calculates average GWP by country and line of business over 2008-2015.

## Prerequisites

- .NET SDK 10.0+
- CSV file available at `C:\Users\rajeshyadav\Downloads\gwpByCountry (2).csv` (default), or override path via config/environment.

## Run

From solution root:

```powershell
dotnet run --project src/CountryGwp.Api/CountryGwp.Api.csproj
```

The API listens on:

- `http://localhost:9091`

OpenAPI document:

- `http://localhost:9091/openapi/v1.json`

## Test

```powershell
dotnet test CountryGwpSolution.sln
```

## Endpoint

- Method: `POST`
- Route: `http://localhost:9091/server/api/gwp/avg`
- Content-Type: `application/json`

Request example:

```json
{
  "country": "ae",
  "lob": ["property", "transport"]
}
```

Response example:

```json
{
  "property": 123456789.0,
  "transport": 446001906.1
}
```

## Notes

- Uses async service/repository calls.
- Uses DI + repository pattern to decouple business logic from data access.
- Loads CSV into memory on first use.
- Caches request results for 10 minutes.
- Includes basic validation and global exception handling.