#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

<#
.SYNOPSIS
    Start an orchestration Azure Function.
.DESCRIPTION
    Start an orchestration Azure Function with the given function name and input value.
    It requires an output binding of the type 'orchestrationClient' is defined for the caller Azure Function.
    If no such output binding is defined, this function throws an exception.
.EXAMPLE
    PS > Start-NewOrchestration -Name starter -InputObject "input value for the orchestration function"
    Return the instance id of the new orchestration.
.PARAMETER FunctionName
    The name of the orchestration Azure Function you want to start.
.PARAMETER InputObject
    The input value that will be passed to the orchestration Azure Function.
.PARAMETER Force
    (Optional) If specified, will force the metadata to be updated for the orchestration Azure Function.
#>
function Start-NewOrchestration {
    [CmdletBinding()]
    param(
        [Parameter(
            Mandatory=$true,
            Position=0,
            ValueFromPipelineByPropertyName=$true)]
        [string] $FunctionName,

        [Parameter(
            Position=2,
            ValueFromPipelineByPropertyName=$true)]
        [object] $InputObject,

        [switch] $Force
    )

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
        Push-KeyValueOutputBinding -Name $starterName -Value $value -Force:$Force.IsPresent
        return $instanceId
    }
    else
    {
        throw "Cannot start an orchestration function. No output binding of the type 'orchestrationClient' was defined."
    }
}
