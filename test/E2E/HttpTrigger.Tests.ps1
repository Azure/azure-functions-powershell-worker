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

    Context "Test basic HttpTrigger function - Ok" {
        BeforeAll {
            $testCases = @(
                @{
                    FunctionName = 'TestBasicHttpTrigger'
                    ExpectedContent = 'Hello Atlas'
                    Parameter = 'Name'
                },
                @{
                    FunctionName = 'TestBasicHttpTriggerWithTriggerMetadata'
                    ExpectedContent = 'Hello Atlas'
                    Parameter = 'name'
                },
                @{
                    FunctionName = 'TestBasicHttpTriggerWithProfile'
                    ExpectedContent = 'PROFILE'
                    Parameter = 'Name'
                },
                @{
                    FunctionName = 'TestHttpTriggerWithEntryPoint'
                    ExpectedContent = 'Hello Atlas'
                    Parameter = 'name'
                },
                @{
                    FunctionName = 'TestHttpTriggerWithEntryPointAndTriggerMetadata'
                    ExpectedContent = 'Hello Atlas'
                    Parameter = 'Name'
                },
                @{
                    FunctionName = 'TestHttpTriggerWithEntryPointAndProfile'
                    ExpectedContent = 'PROFILE'
                    Parameter = 'name'
                }
            )
        }

        It "Http GET request - <FunctionName>" -TestCases $testCases {
            param ($FunctionName, $ExpectedContent, $Parameter)

            $res = Invoke-WebRequest "${FUNCTIONS_BASE_URL}/api/${FunctionName}?${Parameter}=Atlas"

            $res.StatusCode | Should -Be ([HttpStatusCode]::Accepted)
            $res.Content | Should -Be $ExpectedContent
        }

        It "Http POST request - <FunctionName>" -TestCases $testCases {
            param ($FunctionName, $ExpectedContent, $Parameter)

            $res = Invoke-WebRequest "${FUNCTIONS_BASE_URL}/api/${FunctionName}" -Body "{ `"$Parameter`" : `"Atlas`" }" -Method Post -ContentType "application/json"

            $res.StatusCode | Should -Be ([HttpStatusCode]::Accepted)
            $res.Content | Should -Be $ExpectedContent
        }
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
