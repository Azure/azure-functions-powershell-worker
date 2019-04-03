#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

Describe 'Azure Functions PowerShell Langauge Worker Helper Module Tests' {

    BeforeAll {

        ## Create the type 'FunctionMetadata' for our test
        $code = @'
namespace Microsoft.Azure.Functions.PowerShellWorker
{
    using System;
    using System.Collections;

    public class FunctionMetadata
    {
        public static Hashtable GetOutputBindingInfo(Guid guid)
        {
            var hash = new Hashtable(StringComparer.OrdinalIgnoreCase);
            var bi1 = new BindingInfo() { Type = "http", Direction = "out" };
            hash.Add("response", bi1);
            var bi2 = new BindingInfo() { Type = "queue", Direction = "out" };
            hash.Add("queue", bi2);

            var bi3 = new BindingInfo() { Type = "new", Direction = "out" };
            hash.Add("Foo", bi3);
            hash.Add("Bar", bi3);
            hash.Add("Food", bi3);

            return hash;
        }
    }

    public class BindingInfo
    {
        public string Type;
        public string Direction;
    }
}
'@
        $type = "Microsoft.Azure.Functions.PowerShellWorker.FunctionMetadata" -as [Type]
        if ($null -eq $type) {
            Add-Type -TypeDefinition $code
        }

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
                if (!$h2.ContainsKey($key)) {
                    return $false
                }
                if ($h1[$key] -eq $h2[$key]) {
                    continue
                }
                if ($h1[$key] -is [System.Collections.Generic.List[object]] -and $h2[$key] -is [object[]]) {
                    $s1 = $h1[$key] -join ","
                    $s2 = $h2[$key] -join ","
                    if ($s1 -eq $s2) {
                        continue
                    }
                }
                return $false
            }
            return $true
        }
    }

    Context 'Push-OutputBinding tests' {

        AfterAll {
            Get-OutputBinding -Purge > $null
        }

        It 'Can add a value via parameters' {
            $Key = 'queue'

            ## The first item added to 'queue' is the value itself
            Push-OutputBinding -Name $Key -Value 5
            $result = Get-OutputBinding
            $result[$Key] | Should -BeExactly 5

            ## The second item added to 'queue' will make it a list
            Push-OutputBinding -Name $Key -Value 6
            $result = Get-OutputBinding
            $result[$Key] -is [System.Collections.Generic.List[object]] | Should -BeTrue
            $result[$Key][0] | Should -BeExactly 5
            $result[$Key][1] | Should -BeExactly 6

            ## The array added to 'queue' will get unraveled
            Push-OutputBinding -Name $Key -Value @(7, 8)
            $result = Get-OutputBinding -Purge

            $result[$Key] -is [System.Collections.Generic.List[object]] | Should -BeTrue
            $result[$Key][0] | Should -BeExactly 5
            $result[$Key][1] | Should -BeExactly 6
            $result[$Key][2] | Should -BeExactly 7
            $result[$Key][3] | Should -BeExactly 8

            ## The array gets unraveled and added to a list
            Push-OutputBinding -Name $Key -Value @(1, 2)
            $result = Get-OutputBinding -Purge
            $result[$Key] -is [System.Collections.Generic.List[object]] | Should -BeTrue
            $result[$Key][0] | Should -BeExactly 1
            $result[$Key][1] | Should -BeExactly 2
        }

        It 'Can add value with binding name that differs in case' {
            Push-OutputBinding -Name RESPONSE -Value 'UpperCase'
            Push-OutputBinding -Name QUeue -Value 'MixedCase'

            $result = Get-OutputBinding -Purge
            if ($IsWindows) {
                $result["response"] | Should -BeExactly 'UpperCase'
                $result["queue"] | Should -BeExactly 'MixedCase'
            } else {
                # Hashtable on Ubuntu 18.04 server is case-sensitive.
                # It's fixed in 6.2, but the 'pwsh' used in AppVeyor is not 6.2
                $result["RESPONSE"] | Should -BeExactly 'UpperCase'
                $result["QUeue"] | Should -BeExactly 'MixedCase'
            }
        }

        It 'Can add a value via pipeline' {
            'Baz' | Push-OutputBinding -Name response
            'item1', 'item2', 'item3' | Push-OutputBinding -Name queue
            $expected = @{ response = 'Baz'; queue = @('item1', 'item2', 'item3') }
            $result = Get-OutputBinding -Purge
            IsEqualHashtable $result $expected | Should -BeTrue `
                -Because 'The hashtables should be identical'
        }

        It 'Throws if you attempt to overwrite an Output binding' {
            try {
                Push-OutputBinding response 'res'
                { Push-OutputBinding response 'baz' } | Should -Throw
            } finally {
                Get-OutputBinding -Purge > $null
            }
        }

        It 'Throw if you use a non-existent output binding name' {
            { Push-OutputBinding nonexist 'baz' } | Should -Throw
        }

        It 'Can overwrite values if "-Clobber" is specified' {
            Push-OutputBinding response 5
            $result = Get-OutputBinding
            IsEqualHashtable @{response = 5} $result | Should -BeTrue `
                -Because 'The hashtables should be identical'

            Push-OutputBinding response 6 -Clobber
            $result = Get-OutputBinding -Purge
            IsEqualHashtable @{response = 6} $result | Should -BeTrue `
                -Because '-Clobber should let you overwrite the output binding'

            Push-OutputBinding 'queue' 1
            $result = Get-OutputBinding
            $result['queue'] | Should -BeExactly 1

            Push-OutputBinding 'queue' 2 -Clobber
            $result = Get-OutputBinding
            $result['queue'] | Should -BeExactly 2

            Push-OutputBinding 'queue' @(3, 4) -Clobber
            $result = Get-OutputBinding -Purge
            $result['queue'] -is [System.Collections.Generic.List[object]] | Should -BeTrue
            $result['queue'][0] | Should -BeExactly 3
            $result['queue'][1] | Should -BeExactly 4
        }
    }

    Context 'Get-OutputBinding tests' {
        BeforeAll {
            Push-OutputBinding -Name Foo -Value 1
            Push-OutputBinding -Name Bar -Value 'Baz'
            Push-OutputBinding -Name Food -Value 'apple'
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
            $inputData = @{ Foo = 1; Bar = 'Baz'; Food = 'apple'}
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
