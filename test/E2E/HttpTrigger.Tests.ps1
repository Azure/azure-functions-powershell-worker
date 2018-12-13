#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

using namespace System.Net

Describe 'HttpTrigger Tests' {
    BeforeAll {
        $FUNCTIONS_BASE_URL = 'http://localhost:7071'
        & "$PSScriptRoot/setupE2Etests.ps1"
        { Invoke-RestMethod $FUNCTIONS_BASE_URL } | Should -Not -Throw -Because 'The E2E tests require a Function App to be running on port 7071'
    }

    AfterAll {
        Get-Job -Name FuncJob -ErrorAction SilentlyContinue | Stop-Job | Remove-Job
    }

    It "Test basic HttpTrigger function - Ok - <FunctionName>" -TestCases @(
        @{ 
            FunctionName = 'TestBasicHttpTrigger'
            ExpectedContent = 'Hello Atlas'
        },
        @{ 
            FunctionName = 'TestBasicHttpTriggerWithTriggerMetadata'
            ExpectedContent = 'Hello Atlas'
        },
        @{
            FunctionName = 'TestBasicHttpTriggerWithProfile'
            ExpectedContent = 'PROFILE'
        },
		@{
			FunctionName = 'TestHttpTriggerWithEntryPoint'
			ExpectedContent = 'Hello Atlas'
		},
		@{
			FunctionName = 'TestHttpTriggerWithEntryPointAndTriggerMetadata'
			ExpectedContent = 'Hello Atlas'
		},
		@{
			FunctionName = 'TestHttpTriggerWithEntryPointAndProfile'
			ExpectedContent = 'PROFILE'
		}
    ) {
        param ($FunctionName, $ExpectedContent)

        $res = Invoke-WebRequest "$FUNCTIONS_BASE_URL/api/$($FunctionName)?Name=Atlas"
        
        $res.StatusCode | Should -Be ([HttpStatusCode]::Accepted)
        $res.Content | Should -Be $ExpectedContent
    }

    It "Test basic HttpTrigger function - BadRequest - <FunctionName>" -TestCases @(
        @{ 
            FunctionName = 'TestBasicHttpTrigger'
            InputNameData = $null
        },
        @{ 
            FunctionName = 'TestBasicHttpTrigger'
        },
        @{ 
            FunctionName = 'TestBasicHttpTriggerWithTriggerMetadata'
            InputNameData = $null
        },
        @{ 
            FunctionName = 'TestBasicHttpTriggerWithTriggerMetadata'
        }
    ) {
        param ($FunctionName, $InputNameData)

        if (Test-Path 'variable:InputNameData') {
            $url = "$FUNCTIONS_BASE_URL/api/$($FunctionName)?Name=$InputNameData"
        } else {
            $url = "$FUNCTIONS_BASE_URL/api/$($FunctionName)"
        }

        $res = { invoke-webrequest $url } |
            Should -Throw -ExpectedMessage 'Response status code does not indicate success: 400 (Bad Request).' -PassThru
        $res.Exception.Response.StatusCode | Should -Be ([HttpStatusCode]::BadRequest)
    }

    It "Test basic HttpTrigger function - InternalServerError" {
        $res = { invoke-webrequest "$FUNCTIONS_BASE_URL/api/TestBadHttpTrigger" } |
            Should -Throw -ExpectedMessage 'Response status code does not indicate success: 500 (Internal Server Error).' -PassThru
        $res.Exception.Response.StatusCode | Should -Be ([HttpStatusCode]::InternalServerError)
    }
}
