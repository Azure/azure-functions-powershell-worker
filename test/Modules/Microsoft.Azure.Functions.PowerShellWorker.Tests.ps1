#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

Describe 'Azure Functions PowerShell Langauge Worker Helper Module Tests' {

    # Helper function that tests hashtable equality
    function IsEqualHashtable ($h1, $h2) {
        # Handle nulls
        if (!$h1) {
            if(!$h2) {
                return $true
            }
            return $false
        }
        if (!$h1) {
            return $false
        }
    
        # If they don't have the same amount of key value pairs, fail early
        if ($h1.Keys.Count -ne $h2.Keys.Count){
            return $false
        }
    
        # Check to make sure every key exists in the other and that the values are the same
        foreach ($key in $h1.Keys) {
            if (!$h2.ContainsKey($key) -or $h1[$key] -ne $h2[$key]) {
                return $false
            }
        }
        return $true
    }

    Context 'Push-OutputBinding tests' {
        BeforeEach {
            Import-Module "$PSScriptRoot/../../src/Modules/Microsoft.Azure.Functions.PowerShellWorker/Microsoft.Azure.Functions.PowerShellWorker.psd1" -Force
            $module = (Get-Module Microsoft.Azure.Functions.PowerShellWorker)[0]
        }

        It 'Can add a value via parameters' {
            $Key = 'Test'
            $Value = 5

            Push-OutputBinding -Name $Key -Value $Value
            $result = & $module { $script:_OutputBindings }
            $result[$Key] | Should -BeExactly $Value
        }

        It 'Can add a value via pipeline - <Description>' -TestCases @(
        @{
            InputData = @{ Foo = 1; Bar = 'Baz'}
            Expected = @{ Foo = 1; Bar = 'Baz'}
            Description = 'ValueFromPipeline'
        },
        @{
            InputData = @([PSCustomObject]@{ Name = 'Foo'; Value = 5 }, [PSCustomObject]@{ Name = 'Bar'; Value = 'Baz' })
            Expected = @{ Foo = 5; Bar = 'Baz'}
            Description = 'ValueFromPipelineByPropertyName'
        }) -Test {
            param (
                [object] $InputData,
                [hashtable] $Expected,
                [string] $Description
            )

            $InputData | Push-OutputBinding
            $result = & $module { $script:_OutputBindings }
            IsEqualHashtable $result $Expected | Should -BeTrue `
                -Because 'The hashtables should be identical'
        }

        It 'Throws if you attempt to overwrite an Output binding' {
            Push-OutputBinding Foo 5
            { Push-OutputBinding Foo 6} | Should -Throw
        }

        It 'Can overwrite values if "-Force" is specified' {
            $internalHashtable = & $module { $script:_OutputBindings }
            Push-OutputBinding Foo 5
            IsEqualHashtable @{Foo = 5} $internalHashtable | Should -BeTrue `
                -Because 'The hashtables should be identical'

            Push-OutputBinding Foo 6 -Force
            IsEqualHashtable @{Foo = 6} $internalHashtable | Should -BeTrue `
                -Because '-Force should let you overwrite the output binding'
        }
    }

    Context 'Get-OutputBinding tests' {
        BeforeAll {
            Import-Module "$PSScriptRoot/../../src/Modules/Microsoft.Azure.Functions.PowerShellWorker/Microsoft.Azure.Functions.PowerShellWorker.psd1" -Force
            $module = (Get-Module Microsoft.Azure.Functions.PowerShellWorker)[0]
            & $module {
                $script:_OutputBindings = @{ Foo = 1; Bar = 'Baz'; Food = 'apple'}
            }
        }

        It 'Can get the output binding hashmap - <Description>' -TestCases @(
        @{
            Query = @{}
            Expected = @{ Foo = 1; Bar = 'Baz'; Food = 'apple'}
            Description = 'No name specified'
        },
        @{
            Query = @{ Name = 'Foo' }
            Expected = @{ Foo = 1; }
            Description = 'Explicit name specified'
        },
        @{
            Query = @{ Name = 'DoesNotExist' }
            Expected = @{}
            Description = 'Explicit name specified that does not exist'
        },
        @{
            Query = @{ Name = 'F*' }
            Expected = @{ Foo = 1; Food = 'apple' }
            Description = 'Wildcard name specified'
        }) -Test {
            param (
                [object] $Query,
                [hashtable] $Expected,
                [string] $Description
            )

            $result = Get-OutputBinding @Query
            IsEqualHashtable $result $Expected | Should -BeTrue `
                -Because 'The hashtables should be identical'
        }

        It 'Can use the "-Purge" flag to clear the Output bindings' {
            $initialState = (& $module { $script:_OutputBindings }).Clone()

            $result = Get-OutputBinding -Purge
            IsEqualHashtable $result $initialState | Should -BeTrue `
                -Because 'The full hashtable should be returned'

            $newState = & $module { $script:_OutputBindings }
            IsEqualHashtable @{} $newState | Should -BeTrue `
                -Because 'The OutputBindings should be empty'
        }
    }
}
