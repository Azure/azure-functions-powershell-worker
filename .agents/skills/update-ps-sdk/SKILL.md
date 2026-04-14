---
name: update-ps-sdk
description: 'Upgrade the PowerShell SDK version in the Azure Functions PowerShell language worker. Use when: updating PowerShell SDK, upgrading PS version, bumping Microsoft.PowerShell.SDK, updating bundled modules, new PowerShell release.'
argument-hint: 'Target PowerShell SDK version (e.g., 7.6.0-preview.5, 7.4.7)'
---

# Upgrade PowerShell SDK Version

Upgrades the PowerShell language worker to reference a new PowerShell SDK release.

## When to Use

- A new PowerShell SDK version is released on [GitHub](https://github.com/PowerShell/PowerShell/releases)
- You need to bump the `Microsoft.PowerShell.SDK` package version

## Inputs

The user must provide (or you must confirm):
- **Target PS SDK version** — e.g., `7.6.0-preview.5`, `7.4.7`
- **Release tag** — typically `v<version>`, e.g., `v7.6.0-preview.5`

## Procedure

### 1. Check the .NET SDK Requirement

Look up the .NET SDK version required by the target PowerShell SDK. The release notes at `https://github.com/PowerShell/PowerShell/releases/tag/<releaseTag>` list the exact .NET SDK version under **Build and Packaging Improvements** (e.g., "Update .NET SDK to 8.0.419").

- Update the `<TargetFramework>` in both `.csproj` files if the .NET **major** version has changed:
  - `src/Microsoft.Azure.Functions.PowerShellWorker.csproj`
  - `test/Unit/Microsoft.Azure.Functions.PowerShellWorker.Test.csproj`

- Update `MinimalPatch` and `DefaultPatch` in `tools/helper.psm1` (`$DotnetSDKVersionRequirements`) to match the .NET SDK patch version from the release notes. For example, if the release requires .NET SDK `8.0.419`, set both values to `'419'`.

### 2. Update the PowerShell SDK Package Version

Update the `Microsoft.PowerShell.SDK` `<PackageReference>` version in **both** project files:

- `src/Microsoft.Azure.Functions.PowerShellWorker.csproj`
- `test/Unit/Microsoft.Azure.Functions.PowerShellWorker.Test.csproj`

### 3. Update SDK Dependencies

Check the PowerShell release notes for any new or updated dependencies required by the SDK. For example, certain releases require a specific `Microsoft.CodeAnalysis.CSharp` version. Update these in both `.csproj` files as needed.

### 4. Remove Temporarily Pinned Transitives

Review both `.csproj` files for `<PackageReference>` entries that were pinned to work around transitive dependency issues (e.g., a vulnerable transitive that was pinned until the parent package updated it). If the new PowerShell SDK now pulls in a sufficiently new version of that transitive, **remove the explicit pin** rather than bumping it. Only keep explicit pins that are still necessary.

### 5. Update Bundled Module Versions

Update module versions in `src/requirements.psd1` to match the versions shipped with the target release. The authoritative source for bundled module versions is:

```
https://github.com/PowerShell/PowerShell/blob/<releaseTag>/src/Modules/PSGalleryModules.csproj
```

Replace `<releaseTag>` with the actual tag (e.g., `v7.6.0-preview.5`).

The modules to check are listed in `src/requirements.psd1` (e.g., `Microsoft.PowerShell.Archive`, `ThreadJob`, `PowerShellGet`, `PackageManagement`).

### 6. Build and Test

Run a clean build with tests:

```
pwsh -c "./build.ps1 -Clean -Test"
```

**Address all build warnings.** Common warnings to fix:
- `NU1605` (package downgrade) — update the pinned version or remove the pin if the SDK now provides a newer transitive.
- `NU1510` (unnecessary pinned package) — remove the `<PackageReference>` since the package is no longer needed as a direct dependency.
- `CS8632` and other code warnings — fix the underlying code issue.

Re-run the build until it completes with **zero warnings and all tests passing**.

### 7. Check for Vulnerable Packages

Run the vulnerability checker against the solution:

```
pwsh -c "./Check-CsprojVulnerabilities.ps1 -PrintReport"
```

If vulnerabilities are found, update or pin the affected packages to non-vulnerable versions, then re-run steps 6 and 7.

### 8. Submit PR

Submit a pull request to the `dev` branch. The `dev` branch always tracks the latest PowerShell version.
