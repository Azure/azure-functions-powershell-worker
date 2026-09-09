# V2 Programming Model Example App

This example demonstrates the **V2 attribute-based programming model** for Azure Functions with PowerShell. Functions are defined using PowerShell attributes — no `function.json` files are needed.

## Key Differences from V1

| Feature | V1 (Classic) | V2 (Attribute-based) |
|---|---|---|
| Function definition | `function.json` + `run.ps1` per function | `[AzFunction()]` attribute in `.psm1` files |
| File layout | One folder per function | Functions organized by domain in `.psm1` files |
| Binding configuration | JSON in `function.json` | Attributes on parameters |
| Indexing | Host reads `function.json` | Worker indexes via AST parsing |

## Functions Included

### HttpFunctions.psm1
- **HttpExample** — Anonymous HTTP trigger (GET/POST) with custom route `/hello`
- **GetAzureVm** — Function-level auth HTTP trigger that fetches an Azure VM

### TimerFunctions.psm1
- **CleanupJob** — Timer trigger that runs every 6 hours

### QueueFunctions.psm1
- **ProcessQueueMessage** — Queue trigger with queue output binding

## Running Locally

```powershell
cd examples/V2App
func start
```

## .funcignore

The `.funcignore` file excludes files from deployment (similar to `.gitignore`):
- `.git*` — Git metadata
- `.vscode` — Editor settings
- `local.settings.json` — Local-only settings
- `test` — Test directories
- `*.tests.ps1` — Test files
