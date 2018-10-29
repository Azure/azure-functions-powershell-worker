#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

Describe 'HttpTrigger Tests' {
    BeforeAll {
        $FUNCTIONS_BASE_URL = 'http://localhost:7071'
        & "$PSScriptRoot/setupE2Etests.ps1"
        { Invoke-RestMethod $FUNCTIONS_BASE_URL } | Should -Not -Throw -Because 'The E2E tests require a Function App to be running on port 7071'
    }

    AfterAll {
        Get-Job -Name FuncJob -ErrorAction SilentlyContinue | Stop-Job | Remove-Job
    }

    It "Test basic HttpTrigger function" -TestCases @(
        @{ Name = 'Atlas'; StatusCode = 200; Content = 'Hello World' },
        @{ Name = $null; StatusCode = 400 },
        @{ StatusCode = 400 }
    ) -Test {
        param ($Name, $StatusCode, $Content)

        if (Test-Path 'variable:Name') {
            $url = "$FUNCTIONS_BASE_URL/api/TestBasicHttpTrigger?Name=$Name"
        } else {
            $url = "$FUNCTIONS_BASE_URL/api/TestBasicHttpTrigger"
        }


        try {
            $res = Invoke-WebRequest $url
        } catch {
            $res = $_.Exception.Response
        }
        [int]$res.StatusCode | Should -Be $StatusCode

        if ($Content) {
            $res.Content | Should -Be $Content
        }
    }
}
