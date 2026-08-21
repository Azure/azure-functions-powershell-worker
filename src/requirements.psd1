@{
    # Modules bundled with the PowerShell Language Worker
    'Microsoft.PowerShell.Archive' = @{
        Version = '1.2.6'
        Target = 'src/Modules'
        Parameters = @{
            Repository = 'upstream-public'
        }
    }
    'Microsoft.PowerShell.ThreadJob' = @{
        Version = '2.2.0'
        Target = 'src/Modules'
        Parameters = @{
            Repository = 'upstream-public'
        }
    }
    'PowerShellGet' = @{
        Version = '2.2.5'
        Target = 'src/Modules'
        Parameters = @{
            Repository = 'upstream-public'
        }
    }
    'PackageManagement' = @{
        Version = '1.4.8.1'
        Target = 'src/Modules'
        Parameters = @{
            Repository = 'upstream-public'
        }
    }
}
