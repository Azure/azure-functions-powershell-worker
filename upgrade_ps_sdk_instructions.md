##  Instructions for Upgrading the PowerShell Language Worker to a New PowerShell SDK Version
Once a new PowerShell SDK version is released on [GiHub](https://github.com/PowerShell/PowerShell/releases), follow these steps to upgrade the PowerShell SDK reference used by the language worker:

* Update the .NET SDK version used by the language worker to match the version used by the PowerShell SDK. The .NET SDK versions can be found [here](https://dotnet.microsoft.com/en-us/download/dotnet/).
* Upgrade the PowerShell SDK version for both the language worker and the unit test project.
    * Sometimes, you might need to upgrade the dependencies required by the PowerShell SDK. If this is the case, you will find these dependencies in the release notes. For example, the `PowerShell 7.4 SDK Preview` requires the latest `Microsoft.CodeAnalysis.CSharp` package.
* Upgrade the modules in the `src/requirements.psd1` file to match the versions in the release. These are available at `https://github.com/PowerShell/PowerShell/blob/<releaseTag>/src/Modules/PSGalleryModules.csproj`, for instance, `https://github.com/PowerShell/PowerShell/blob/v7.4.0-preview.5/src/Modules/PSGalleryModules.csproj`.
* Once these steps are completed, submit a pull request (PR). Please note that the `dev` branch always references the latest PowerShell version, such as `PowerShell 7.4`.

