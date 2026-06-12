# 4. RpCCTransferJsonToDb

Loads structured customer-call analysis (the JSON produced by projects #2/#3)
into a SQL Server table. Also ships a `generate` mode that inserts fake rows
via [Bogus](https://github.com/bchavez/Bogus) for downstream testing of the
analytics / query projects.

> **No AI / no MAF here.** This is a plain .NET 9 modernization — strongly-typed
> config, async ADO.NET, transactional batch inserts, schema bootstrap on
> startup. It's the persistence layer that the rest of the pipeline writes
> into and reads from.

## Stack

| Concern              | Library                          |
| -------------------- | -------------------------------- |
| Target framework     | `.NET 9`                         |
| SQL Server client    | `Microsoft.Data.SqlClient` 5.2.2 |
| Fake data            | `Bogus` 35.6.1                   |
| Configuration        | `Microsoft.Extensions.Configuration[.Json/.Binder]` 9.0.3 |

## Database

Default target is **localhost SQL Server, database `Call_Center`, integrated
(Windows) auth**. The connection string is in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "CallCenter": "Server=localhost;Database=Call_Center;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;"
  }
}
```

Override per-developer via `appsettings.Local.json` (git-ignored):

```json
{
  "ConnectionStrings": {
    "CallCenter": "Server=tcp:my-azure-sql.database.windows.net,1433;Database=Call_Center;Authentication=Active Directory Default;Encrypt=True;"
  }
}
```

### One-time setup

```powershell
sqlcmd -S localhost -E -Q "IF DB_ID('Call_Center') IS NULL CREATE DATABASE Call_Center"
```

The `CustomerIssues` table is created automatically on first run via
`CallCenterRepository.EnsureSchemaAsync()`. The same DDL is also checked in
as `schema.sql` for reference.

Table layout:

| Column                     | Type             | Notes                                |
| -------------------------- | ---------------- | ------------------------------------ |
| Id                         | INT IDENTITY PK  |                                      |
| ClassifiedReason           | NVARCHAR(64)     | e.g. `late_package`, `damaged_package` |
| ResolveStatus              | NVARCHAR(32)     | `resolved` / `unresolved` / `pending` |
| CallSummary                | NVARCHAR(500)    |                                      |
| CustomerName / EmployeeName| NVARCHAR(128)    |                                      |
| OrderNumber                | NVARCHAR(32)     |                                      |
| CustomerContactNr          | NVARCHAR(32)     |                                      |
| NewAddress                 | NVARCHAR(256)    |                                      |
| SentimentInitial / Final   | NVARCHAR(128)    | comma-joined string array            |
| SatisfactionScoreInitial / Final | INT        | 0–10                                 |
| Eta                        | NVARCHAR(32)     |                                      |
| ActionItem                 | NVARCHAR(256)    | comma-joined string array            |
| CallDate                   | DATE             |                                      |
| InsertedAt                 | DATETIME2        | defaults to `SYSUTCDATETIME()`       |

## Run

### Insert one record from `output.json`

```powershell
cd "4. RpCCTransferJsonToDb"
dotnet run
```

Reads `output.json` (a sample analysis matching `CustomerIssue`) and inserts
a single row.

### Generate fake rows

```powershell
dotnet run -- generate 100
```

Inserts 100 randomly-generated rows inside a single transaction.

## Project layout

```
4. RpCCTransferJsonToDb/
├── Configuration/
│   └── AppSettings.cs               ConnectionStrings:CallCenter
├── Services/
│   └── CallCenterRepository.cs      EnsureSchemaAsync, InsertAsync, InsertManyAsync
├── CustomerIssue.cs                 DTO matching the JSON shape from #2/#3
├── Generator.cs                     Bogus-driven fake-row factory
├── Program.cs                       single dispatch: load JSON or generate
├── appsettings.json                 localhost connection string (committed)
├── appsettings.Local.json           per-developer override (.gitignored)
├── schema.sql                       reference DDL
├── output.json                      sample input
└── RpCCTransferJsonToDb.csproj
```

## Where this fits in the pipeline

```
[#1 Audio → transcript]  →  [#2/#3 transcript → JSON]  →  [#4 JSON → SQL]  →  [#5+ analytics]
```

Projects #5–#8 will read from this same `Call_Center.CustomerIssues` table
to drive multi-agent analytics, retrieval, and reporting.
