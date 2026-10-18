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
## Scoped read repositories

`ScopedReadRepository<TEntity,TKey>` supports string and composite key contracts without changing existing numeric CRUD bases. Hosts supply `IReadScope<TEntity>`; derived/generated repositories supply a key-match expression. Every read applies the scope to an untracked query, including when another entity with that key is already tracked. No saves or implicit inserts occur. Compose commands in explicit host workflows using one unit of work; this API deliberately does not infer create/update intent from assigned GUIDs.

## Repository ownership (2026-10-18)

The numeric CRUD base owns property lookup, tracked property updates and soft/hard deletion,
as well as its existing query/order/save helpers. Consumers must not copy these methods into
application bases. Property updates use the virtual PersistEntityAsync hook, so each host
retains its audited DbContext save behavior. Host bases may supply their typed context and
that audit override; feature queries/commands belong in custom repository partials.

Query isolation remains a host responsibility through EF query filters (CRUD profile) or
the mandatory IReadScope (scoped-read profile). This consolidation does not make arbitrary
CRUD safe for security entities or change assigned string/composite identifiers.

Run the isolated shared regression suite with:

    dotnet test tests/BrighterTools.Data.EFCore.Tests/BrighterTools.Data.EFCore.Tests.csproj

No schema migration is needed for this source change. Local consumers need the updated
shared source and host changes together; no package release was published.
