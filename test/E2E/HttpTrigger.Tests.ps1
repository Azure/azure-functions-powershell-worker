#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

Describe 'HttpTrigger Tests' {
    BeforeAll {
        & "$PSScriptRoot/setupE2Etests.ps1"
        { Invoke-RestMethod 'http://localhost:7071' } | Should -Not -Throw -Because 'The E2E tests require a Function App to be running on port 7071'
    }
    AfterAll {
        Get-Job -Name FuncJob -ErrorAction SilentlyContinue | Stop-Job | Remove-Job
    }
    It 'Simple' {
        Invoke-RestMethod 'http://localhost:7071/api/MyHttpTrigger?Name=Atlas' | Should -Be 'Hello Atlas'
    }
}
