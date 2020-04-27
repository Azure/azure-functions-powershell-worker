#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function CheckIfDurableFunctionsEnabled {
    if (($null -ne $env:PSWorkerEnableExperimentalDurableFunctions) -and
            (-not [bool]::Parse($env:PSWorkerEnableExperimentalDurableFunctions))) {
		throw 'Durable function is not yet supported for PowerShell.'
	}
}

function GetDurableClientFromModulePrivateData {
    $PrivateData = $PSCmdlet.MyInvocation.MyCommand.Module.PrivateData
    if ($PrivateData) {
        $PrivateData['DurableClient']
    }
}

<#
.SYNOPSIS
    Start an orchestration Azure Function.
.DESCRIPTION
    Start an orchestration Azure Function with the given function name and input value.
.EXAMPLE
    PS > Start-NewOrchestration -DurableClient Starter -FunctionName OrchestratorFunction -InputObject "input value for the orchestration function"
    Return the instance id of the new orchestration.
.PARAMETER FunctionName
    The name of the orchestration Azure Function you want to start.
.PARAMETER InputObject
    The input value that will be passed to the orchestration Azure Function.
.PARAMETER DurableClient
    The orchestration client object.
#>
function Start-NewOrchestration {
    [CmdletBinding()]
    param(
        [Parameter(
            Mandatory=$true,
            Position=0,
            ValueFromPipelineByPropertyName=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $FunctionName,

        [Parameter(
            Position=1,
            ValueFromPipelineByPropertyName=$true)]
        [object] $InputObject,

		[Parameter(
            ValueFromPipelineByPropertyName=$true)]
        [object] $DurableClient
    )

    CheckIfDurableFunctionsEnabled
    
    if ($null -eq $DurableClient) {
        $DurableClient = GetDurableClientFromModulePrivateData
        if ($null -eq $DurableClient) {
            throw "Cannot start an orchestration function. No binding of the type 'durableClient' was defined."
        }
    }

    $InstanceId = (New-Guid).Guid

    $UriTemplate = $DurableClient.creationUrls.createNewInstancePostUri
    $Uri = $UriTemplate.Replace('{functionName}', $FunctionName).Replace('[/{instanceId}]', "/$InstanceId")
    $Body = $InputObject | ConvertTo-Json -Compress
              
    $null = Invoke-RestMethod -Uri $Uri -Method 'POST' -ContentType 'application/json' -Body $Body
    
    return $instanceId
}

function IsValidUrl([uri]$Url) {
    $Url.IsAbsoluteUri -and ($Url.Scheme -in 'http', 'https')
}

function GetUrlOrigin([uri]$Url) {
    $fixedOriginUrl = New-Object System.UriBuilder
    $fixedOriginUrl.Scheme = $Url.Scheme
    $fixedOriginUrl.Host = $Url.Host
    $fixedOriginUrl.Port = $Url.Port
    $fixedOriginUrl.ToString()
}

function New-OrchestrationCheckStatusResponse {
    [CmdletBinding()]
    param(
        [Parameter(
            Mandatory=$true,
            ValueFromPipelineByPropertyName=$true)]
        [object] $Request,

        [Parameter(
            Mandatory=$true,
            ValueFromPipelineByPropertyName=$true)]
        [string] $InstanceId,

		[Parameter(
            ValueFromPipelineByPropertyName=$true)]
        [object] $DurableClient
    )
    
	CheckIfDurableFunctionsEnabled

    if ($null -eq $DurableClient) {
        $DurableClient = GetDurableClientFromModulePrivateData
        if ($null -eq $DurableClient) {
            throw "Cannot create orchestration check status response. No binding of the type 'durableClient' was defined."
        }
    }

    [uri]$requestUrl = $Request.Url
    $requestHasValidUrl = IsValidUrl $requestUrl
    $requestUrlOrigin = GetUrlOrigin $requestUrl
    
    $httpManagementPayload = @{ }
    foreach ($entry in $DurableClient.managementUrls.GetEnumerator()) {
        $value = $entry.Value
    
        if ($requestHasValidUrl -and (IsValidUrl $value)) {
            $dataOrigin = GetUrlOrigin $value
            $value = $value.Replace($dataOrigin, $requestUrlOrigin)
        }
      
        $value = $value.Replace($DurableClient.managementUrls.id, $InstanceId)
        $httpManagementPayload.Add($entry.Name, $value)
    }
    
    [HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::Accepted
        Body = $httpManagementPayload
        Headers = @{
            'Content-Type' = 'application/json'
            'Location' = $httpManagementPayload.statusQueryGetUri
            'Retry-After' = 10
        }
    }
}
