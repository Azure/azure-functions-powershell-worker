# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

BeforeAll {
    $buildScript = Join-Path $PSScriptRoot '../../build.ps1'
    Import-Module (Join-Path $PSScriptRoot '../../tools/helper.psm1') -Force
    Mock Import-Module {} -ParameterFilter { $Name -like '*helper.psm1' }
    Mock Find-Dotnet {}
    . $buildScript -NoBuild
}

Describe 'Pinned bootstrap modules' {
    BeforeEach {
        Mock Install-Dotnet {}
        Mock Get-PSRepository { @{ Name = 'upstream-public' } }
        Mock Set-PSRepository {}
        Mock Install-Module {}
        Mock Get-Module {} -ParameterFilter { $ListAvailable -and $FullyQualifiedName }
    }

    It 'installs exact versions from CFS rather than selecting the latest upstream release' {
        . $buildScript -NoBuild -Bootstrap

        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'PSDepend' -and $RequiredVersion -eq '0.4.1' -and
            $Repository -eq 'upstream-public' -and $Scope -eq 'CurrentUser' -and $Force
        }
        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'platyPS' -and $RequiredVersion -eq '0.14.2' -and
            $Repository -eq 'upstream-public' -and $Scope -eq 'CurrentUser' -and $Force
        }
        Should -Invoke Install-Module -Times 2 -Exactly
    }

    It 'checks for the exact installed versions, not just the module names' {
        . $buildScript -NoBuild -Bootstrap

        Should -Invoke Get-Module -Times 1 -Exactly -ParameterFilter {
            $ListAvailable -and $FullyQualifiedName.Name -eq 'PSDepend' -and
            $FullyQualifiedName.RequiredVersion -eq '0.4.1'
        }
        Should -Invoke Get-Module -Times 1 -Exactly -ParameterFilter {
            $ListAvailable -and $FullyQualifiedName.Name -eq 'platyPS' -and
            $FullyQualifiedName.RequiredVersion -eq '0.14.2'
        }
    }

    It 'does not reinstall the pinned versions when they are already available' {
        Mock Get-Module { @{ Version = $FullyQualifiedName.RequiredVersion } } -ParameterFilter {
            $ListAvailable -and $FullyQualifiedName
        }

        . $buildScript -NoBuild -Bootstrap

        Should -Invoke Install-Module -Times 0 -Exactly
    }

    It 'requires the pinned PSDepend version for builds without bootstrap' {
        { . $buildScript } | Should -Throw "*PSDepend*0.4.1*-Bootstrap*"
    }

    It 'imports the pinned PSDepend version instead of autoloading a newer installed version' {
        Mock Get-Module { @{ Version = '0.4.1' } } -ParameterFilter {
            $ListAvailable -and $FullyQualifiedName
        }
        Mock Import-Module { throw 'Stop before building' } -ParameterFilter { $Name -eq 'PSDepend' }

        { . $buildScript } | Should -Throw 'Stop before building'

        Should -Invoke Import-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'PSDepend' -and $RequiredVersion -eq '0.4.1' -and $Force
        }
    }
}

Describe 'CFS installation failures' {
    BeforeEach {
        Mock Start-Sleep {}
    }

    It 'preserves the version pin when retrying a package that has not been ingested yet' {
        $script:attempts = 0
        Mock Install-Module {
            $script:attempts++
            if ($script:attempts -eq 1) {
                $exception = [System.InvalidOperationException]::new('Package not found')
                throw [System.Management.Automation.ErrorRecord]::new(
                    $exception, 'NoMatchFoundForCriteria,Install-Module',
                    [System.Management.Automation.ErrorCategory]::ObjectNotFound, $Name)
            }
        }

        Install-CfsModule -Name PSDepend -RequiredVersion '0.4.1'

        Should -Invoke Install-Module -Times 2 -Exactly -ParameterFilter {
            $Name -eq 'PSDepend' -and $RequiredVersion -eq '0.4.1' -and
            $Repository -eq 'upstream-public'
        }
        Should -Invoke Start-Sleep -Times 1 -Exactly -ParameterFilter { $Seconds -eq 15 }
    }

    It 'fails after four attempts rather than falling back to another version or repository' {
        Mock Install-Module {
            $exception = [System.InvalidOperationException]::new('Package not found')
            throw [System.Management.Automation.ErrorRecord]::new(
                $exception, 'NoMatchFoundForCriteria,Install-Module',
                [System.Management.Automation.ErrorCategory]::ObjectNotFound, $Name)
        }

        { Install-CfsModule -Name PSDepend -RequiredVersion '0.4.1' } | Should -Throw '*Package not found*'

        Should -Invoke Install-Module -Times 4 -Exactly -ParameterFilter {
            $RequiredVersion -eq '0.4.1' -and $Repository -eq 'upstream-public'
        }
        Should -Invoke Start-Sleep -Times 3 -Exactly
    }

    It 'surfaces invalid package errors without retrying or selecting another release' {
        Mock Install-Module { throw 'End of Central Directory record could not be found.' }

        { Install-CfsModule -Name PSDepend -RequiredVersion '0.4.1' } |
            Should -Throw '*End of Central Directory*'

        Should -Invoke Install-Module -Times 1 -Exactly
        Should -Invoke Start-Sleep -Times 0 -Exactly
    }
}
