#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

Describe 'Azure Functions PowerShell Langauge Worker Helper Module Tests' {

    BeforeAll {
        # Move the .psd1 and .psm1 files to the publish folder so that the dlls can be found
        $binFolder = Resolve-Path -Path "$PSScriptRoot/../bin"
        $workerDll = Get-ChildItem -Path $binFolder -Filter "Microsoft.Azure.Functions.PowerShellWorker.dll" -Recurse | Select-Object -First 1

        $moduleFolder = Join-Path -Path $workerDll.Directory.FullName -ChildPath "Modules\Microsoft.Azure.Functions.PowerShellWorker"
        $modulePath = Join-Path -Path $moduleFolder -ChildPath "Microsoft.Azure.Functions.PowerShellWorker.psd1"
        Import-Module $modulePath

        # Helper function that tests hashtable equality
        function IsEqualHashtable ($h1, $h2) {
            # Handle nulls
            if (!$h1) {
                if(!$h2) {
                    return $true
                }
                return $false
            }
            if (!$h2) {
                return $false
            }

            # If they don't have the same amount of key value pairs, fail early
            if ($h1.Count -ne $h2.Count){
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
    }

    Context 'Push-OutputBinding tests' {

        AfterAll {
            Get-OutputBinding -Purge > $null
        }

        It 'Can add a value via parameters' {
            $Key = 'Test'
            $Value = 5

            Push-OutputBinding -Name $Key -Value $Value
            $result = Get-OutputBinding -Purge
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
            $result = Get-OutputBinding -Purge
            IsEqualHashtable $result $Expected | Should -BeTrue `
                -Because 'The hashtables should be identical'
        }

        It 'Throws if you attempt to overwrite an Output binding' {
            try {
                Push-OutputBinding Foo 5
                { Push-OutputBinding Foo 6} | Should -Throw
            } finally {
                Get-OutputBinding -Purge > $null
            }
        }

        It 'Can overwrite values if "-Force" is specified' {
            Push-OutputBinding Foo 5
            $result = Get-OutputBinding -Purge
            IsEqualHashtable @{Foo = 5} $result | Should -BeTrue `
                -Because 'The hashtables should be identical'

            Push-OutputBinding Foo 6 -Force
            $result = Get-OutputBinding -Purge
            IsEqualHashtable @{Foo = 6} $result | Should -BeTrue `
                -Because '-Force should let you overwrite the output binding'
        }
    }

    Context 'Get-OutputBinding tests' {
        BeforeAll {
            $inputData = @{ Foo = 1; Bar = 'Baz'; Food = 'apple'}
            $inputData | Push-OutputBinding
        }

        AfterAll {
            Get-OutputBinding -Purge
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
            $result = Get-OutputBinding -Purge
            IsEqualHashtable $result $inputData | Should -BeTrue `
                -Because 'The full hashtable should be returned'

            $newState = Get-OutputBinding
            IsEqualHashtable @{} $newState | Should -BeTrue `
                -Because 'The OutputBindings should be empty'
        }
    }

    Context 'Trace-PipelineObject tests' {
        BeforeAll {
            $scriptToRun = @'
    param($cmd, $modulePath)
    Import-Module $modulePath
    function Write-TestObject {
        foreach ($i in 1..20) {
            Write-Output $cmd
        }
        Write-Information '__LAST_INFO_MSG__'
    }
'@
            $cmd = Get-Command Get-Command
            $ps = [powershell]::Create()
            $ps.AddScript($scriptToRun).AddParameter("cmd", $cmd).AddParameter("modulePath", $modulePath).Invoke()
            $ps.Commands.Clear()
            $ps.Streams.ClearStreams()

            function Write-TestObject {
                foreach ($i in 1..20) {
                    Write-Output $cmd
                }
                Write-Information '__LAST_INFO_MSG__'
            }
        }

        AfterAll {
            $ps.Dispose()
        }

        AfterEach {
            $ps.Commands.Clear()
            $ps.Streams.ClearStreams()
        }

        It "Can write tracing to information stream while keeps input object in pipeline" {
            $results = $ps.AddCommand("Write-TestObject").AddCommand("Trace-PipelineObject").Invoke()

            $results.Count | Should -BeExactly 20
            for ($i = 0; $i -lt 20; $i++) {
                $results[0].Name | Should -BeExactly $cmd.Name
            }

            $outStringResults = Write-TestObject | Out-String -Stream
            $ps.Streams.Information.Count | Should -BeExactly ($outStringResults.Count + 1)

            $lastNonWhitespaceItem = $outStringResults.Count - 1
            while ([string]::IsNullOrWhiteSpace($outStringResults[$lastNonWhitespaceItem])) {
                $lastNonWhitespaceItem--
            }

            for ($i = 0; $i -le $lastNonWhitespaceItem; $i++) {
                $ps.Streams.Information[$i].MessageData | Should -BeExactly $outStringResults[$i]
                $ps.Streams.Information[$i].Tags | Should -BeExactly "__PipelineObject__"
            }

            $ps.Streams.Information[$i].MessageData | Should -BeExactly "__LAST_INFO_MSG__"
            $ps.Streams.Information[$i].Tags | Should -BeNullOrEmpty
        }
    }
}
