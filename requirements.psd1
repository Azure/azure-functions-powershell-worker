@{
    # Packaged with the PowerShell Language Worker
    'PowerShellGet' = @{
        Version = '1.6.7'
        Target = 'src/Modules'
    }
    'Microsoft.PowerShell.Archive' = @{
        Version = '1.1.0.0'
        Target = 'src/Modules'
    }
    'AzureRM' = @{
        Version = '6.8.1'
        Target = 'src/Modules'
    }

    # Dev dependencies
    'Pester' = @{
        Version = 'latest'
        Target = 'CurrentUser'
    }
}
