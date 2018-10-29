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

    It "Test basic HttpTrigger function - Success" -TestCases @(
        @{ 
            FunctionName = 'TestBasicHttpTrigger'
            InputNameData = 'Atlas'
            ExpectedStatusCode = 200
            ExpectedContent = 'Hello Atlas'
        },
        @{ 
            FunctionName = 'TestBasicHttpTrigger'
            InputNameData = $null
            ExpectedStatusCode = 400
        },
        @{ 
            FunctionName = 'TestBasicHttpTrigger'
            ExpectedStatusCode = 400
        },
        @{ 
            FunctionName = 'TestBasicHttpTriggerWithTriggerMetadata'
            InputNameData = 'Atlas'
            ExpectedStatusCode = 200
            ExpectedContent = 'Hello Atlas'
        },
        @{ 
            FunctionName = 'TestBasicHttpTriggerWithTriggerMetadata'
            InputNameData = $null
            ExpectedStatusCode = 400
        },
        @{ 
            FunctionName = 'TestBasicHttpTriggerWithTriggerMetadata'
            ExpectedStatusCode = 400
        }
    ) -Test {
        param ($FunctionName, $InputNameData, $ExpectedStatusCode, $ExpectedContent)

        if (Test-Path 'variable:InputNameData') {
            $url = "$FUNCTIONS_BASE_URL/api/TestBasicHttpTrigger?Name=$InputNameData"
        } else {
            $url = "$FUNCTIONS_BASE_URL/api/TestBasicHttpTrigger"
        }


        try {
            $res = Invoke-WebRequest $url
        } catch {
            $res = $_.Exception.Response
        }
        [int]$res.StatusCode | Should -Be $ExpectedStatusCode

        if ($Content) {
            $res.Content | Should -Be $ExpectedContent
        }
    }

    It "Test basic HttpTrigger function - Error" {
        try {
            Invoke-WebRequest "$FUNCTIONS_BASE_URL/api/TestBadHttpTrigger"
        } catch {
            $res = $_.Exception.Response
        }

        [int]$res.StatusCode | Should -Be 500
    }
}
