# BrighterTools.Data

Shared data contracts and EF Core repository foundations for BrighterTools-based applications.

These packages provide the common entity, request/result, tenant, and repository primitives that BrighterTools-style application data layers can build on. They are designed to pair with `BrighterTools.CodeGenerator` output while keeping application persistence, DbContext shape, authorization, and audit behavior owned by the consuming app.

## Packages

- `BrighterTools.Data.Abstractions`
- `BrighterTools.Data.EFCore`

## Included primitives

- `IEntity<TKey>`
- `ListRequest`
- `ServiceResult<T>`
- `BaseRepository<TEntity, TKey, TUserId>` with tenant enforcement and list filtering helpers

## Install

```powershell
dotnet add package BrighterTools.Data.Abstractions
dotnet add package BrighterTools.Data.EFCore
```

## Design notes

- `ServiceResult<T>` and `ListRequest` are aligned with current app implementations.
- `BaseRepository` is application-agnostic and uses extension points (`OnBeforeSaveInternal`, `PersistEntityAsync`) for app-specific audit persistence.

## More Documentation

- [usage.md](./usage.md) for consuming application guidance
- [publishing.md](./publishing.md) for NuGet packaging and publishing
- [RELEASE_NOTES.md](./RELEASE_NOTES.md) for release history

## Development

```powershell
dotnet restore .\BrighterTools.Data.slnx --configfile .\NuGet.config
dotnet build .\BrighterTools.Data.slnx -c Release --no-restore
```