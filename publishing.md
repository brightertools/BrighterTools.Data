# BrighterTools.Data Publishing

This guide is for maintainers packaging and publishing `BrighterTools.Data.Abstractions` and `BrighterTools.Data.EFCore` to NuGet.

Package pages:

- [BrighterTools.Data.Abstractions](https://www.nuget.org/packages/BrighterTools.Data.Abstractions)
- [BrighterTools.Data.EFCore](https://www.nuget.org/packages/BrighterTools.Data.EFCore)

## Prerequisites

- NuGet Trusted Publishing policies are active for both package IDs.
- Both policies point to repository owner `brightertools`, repository `BrighterTools.Data`, workflow `publish-tool.yml`, and environment `production`.
- The GitHub workflow uses `NuGet/login@v1` and GitHub OIDC.

## Local Packaging

```powershell
dotnet restore .\BrighterTools.Data.slnx --configfile .\NuGet.config
dotnet build .\BrighterTools.Data.slnx -c Release --no-restore
dotnet pack .\src\BrighterTools.Data.Abstractions\BrighterTools.Data.Abstractions.csproj -c Release --no-build --output .\artifacts\nuget --configfile .\NuGet.config
dotnet pack .\src\BrighterTools.Data.EFCore\BrighterTools.Data.EFCore.csproj -c Release --no-build --output .\artifacts\nuget --configfile .\NuGet.config
```

Expected artifacts:

- `artifacts/nuget/BrighterTools.Data.Abstractions.1.0.0.nupkg`
- `artifacts/nuget/BrighterTools.Data.Abstractions.1.0.0.snupkg`
- `artifacts/nuget/BrighterTools.Data.EFCore.1.0.0.nupkg`
- `artifacts/nuget/BrighterTools.Data.EFCore.1.0.0.snupkg`

## Versioning

- Package version is set in `Directory.Build.props` through `VersionPrefix`.
- Use the optional workflow `version` input only when intentionally overriding the checked-in version for a publish run.
- Both packages should normally ship together because the EF Core package builds on the abstractions package.

## GitHub Actions Publishing

Publishing is handled by `.github/workflows/publish-tool.yml`.

Workflow inputs:

- `publish_to_nuget`
- `version` as an optional override

The workflow restores, builds, packs both packages, uploads artifacts, and publishes with `dotnet nuget push --skip-duplicate`. If one package version already exists and the other does not, NuGet skips the duplicate and publishes the missing package.

## Release Checklist

1. Update package metadata and release notes if needed.
2. Run local restore, build, and pack.
3. Confirm both `.nupkg` and `.snupkg` artifacts are produced.
4. Commit and push release changes.
5. Run `publish-tool.yml` with `publish_to_nuget = true`.
6. Verify both packages appear on nuget.org.

## Related Docs

- [README.md](./README.md) for overview and package layout
- [usage.md](./usage.md) for consuming application guidance
- [RELEASE_NOTES.md](./RELEASE_NOTES.md) for release history
