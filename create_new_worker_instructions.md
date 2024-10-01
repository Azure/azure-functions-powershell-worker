##  Instructions for Upgrading the PowerShell Language Worker to a New PowerShell SDK Minor Version (7.6+)
Once a new PowerShell SDK version is released on [GiHub](https://github.com/PowerShell/PowerShell/releases), follow these steps to upgrade the PowerShell SDK reference used by the language worker:

- Update the solution targets as needed for whatever .NET version is targeted by the new PowerShell 
- Follow instructions in upgrade_ps_sdk_instructions.md to update loosely linked dependencies in project files
- Update the Managed Dependency shutoff date in src/DependencyManagement/WorkerEnvironment.cs
