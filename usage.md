# BrighterTools.Data Usage

This guide is for applications consuming `BrighterTools.Data.Abstractions` and `BrighterTools.Data.EFCore`.

Install the shared contracts package when you need entity contracts, list request metadata, or service result wrappers:

```powershell
dotnet add package BrighterTools.Data.Abstractions
```

Install the EF Core package when you want the reusable repository base classes:

```powershell
dotnet add package BrighterTools.Data.EFCore
```

## Package Roles

- `BrighterTools.Data.Abstractions` contains `IEntity<TKey>`, `ListRequest`, and `ServiceResult<T>`.
- `BrighterTools.Data.EFCore` contains `ITenantContextAccessor`, `IBaseRepository<TEntity,TKey,TUserId>`, and `BaseRepository<TEntity,TKey,TUserId>`.
- `BrighterTools.Data.EFCore` references `BrighterTools.Data.Abstractions`, so EF Core consumers normally install the EF Core package in repository projects and the abstractions package where only contracts are needed.

## CodeGenerator Relationship

`BrighterTools.CodeGenerator` generates BrighterTools-style data layer code for consuming applications. These Data packages provide the foundational contracts and base repository types that generated repositories and services can be aligned with.

The generator remains a build-time tool. The consuming application owns whether to reference these packages, which DbContext to use, how entities are modeled, and how audit, authorization, tenancy, and persistence policies are enforced.

## Repository Base Usage

Derive app repositories from `BaseRepository<TEntity,TKey,TUserId>` when the entity implements `IEntity<TKey>` and follows the expected optional conventions:

- `TenantId` for tenant enforcement
- `Order` for ordering and reorder helpers
- `Deleted` for soft-delete support

Override `OnBeforeSaveInternal`, `OnAfterSave`, or `PersistEntityAsync` when the app needs custom audit, validation, or save behavior.

`ITenantContextAccessor` supplies the current tenant ID to the repository. If no accessor is supplied, the repository will not assign a tenant automatically, but entities with a `TenantId` property still need a non-zero tenant value before save.

## List And Result Contracts

Use `ListRequest` to pass paging, sorting, search text, foreign key filters, and enum filters through generated or app-owned list workflows.

Use `ServiceResult<T>` for simple service-layer success/failure responses where the app wants content plus user-facing or diagnostic messages without taking a dependency on a specific web response envelope.

For package publishing, see [publishing.md](./publishing.md).
