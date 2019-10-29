#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function CheckIfDurableFunctionsEnabled {
	if (-not [bool]$env:PSWorkerEnableExperimentalDurableFunctions) {
		throw 'Durable function is not yet supported for PowerShell.'
	}
}

<#
.SYNOPSIS
    Start an orchestration Azure Function.
.DESCRIPTION
    Start an orchestration Azure Function with the given function name and input value.
    It requires an output binding of the type 'orchestrationClient' is defined for the caller Azure Function.
    If no such output binding is defined, this function throws an exception.
.EXAMPLE
    PS > Start-NewOrchestration -FunctionName starter -InputObject "input value for the orchestration function"
    Return the instance id of the new orchestration.
.PARAMETER FunctionName
    The name of the orchestration Azure Function you want to start.
.PARAMETER InputObject
    The input value that will be passed to the orchestration Azure Function.
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
            Position=2,
            ValueFromPipelineByPropertyName=$true)]
        [object] $InputObject
    )

	CheckIfDurableFunctionsEnabled

    $privateData = $PSCmdlet.MyInvocation.MyCommand.Module.PrivateData
    if ($privateData -is [hashtable])
    {
        $starterName = $privateData["OrchestrationStarter"]
    }

    if ($null -ne $starterName)
    {
        $instanceId = (New-Guid).Guid
        $value = ,@{
            FunctionName = $FunctionName
            Input = $InputObject
            InstanceId = $instanceId
        }

        $value = ConvertTo-Json -InputObject $value -Depth 10 -Compress
        Push-OutputBinding -Name $starterName -Value $value
        return $instanceId
    }
    else
    {
        throw "Cannot start an orchestration function. No output binding of the type 'orchestrationClient' was defined."
    }
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

function New-OrchestrationCheckStatusResponse($Request, $OrchestrationClientData, $InstanceId) {
    
	CheckIfDurableFunctionsEnabled

    [uri]$requestUrl = $Request.Url
    $requestHasValidUrl = IsValidUrl $requestUrl
    $requestUrlOrigin = GetUrlOrigin $requestUrl
    
    $httpManagementPayload = @{ }
    foreach ($entry in $OrchestrationClientData.managementUrls.GetEnumerator()) {
        $value = $entry.Value
    
        if ($requestHasValidUrl -and (IsValidUrl $value)) {
            $dataOrigin = GetUrlOrigin $value
            $value = $value.Replace($dataOrigin, $requestUrlOrigin)
        }
      
        $value = $value.Replace($OrchestrationClientData.managementUrls.id, $InstanceId)
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
