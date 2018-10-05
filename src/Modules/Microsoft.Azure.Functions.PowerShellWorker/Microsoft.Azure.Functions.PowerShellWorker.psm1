#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# This holds the current state of the output bindings
$script:_OutputBindings = @{}

<#
.SYNOPSIS
    Gets the hashtable of the output bindings set so far.
.DESCRIPTION
    Gets the hashtable of the output bindings set so far.
.EXAMPLE
    PS > Get-OutputBinding
    Gets the hashtable of all the output bindings set so far.
.EXAMPLE
    PS > Get-OutputBinding -Name res
    Gets the hashtable of specific output binding.
.EXAMPLE
    PS > Get-OutputBinding -Name r*
    Gets the hashtable of output bindings that match the wildcard.
.PARAMETER Name
    The name of the output binding you want to get. Supports wildcards.
.OUTPUTS
    The hashtable of binding names to their respective value.
#>
function Get-OutputBinding {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline = $True, ValueFromPipelineByPropertyName = $True)]
        [string[]]
        $Name = '*',

        [switch]
        $Purge
    )
    begin {
        $bindings = @{}
    }
    process {
        $script:_OutputBindings.GetEnumerator() | Where-Object Name -Like $Name | ForEach-Object { $null = $bindings.Add($_.Name, $_.Value) }
    }
    end {
        if($Purge.IsPresent) {
            $script:_OutputBindings.Clear()
        }
        return $bindings
    }
}

# Helper private function that sets an OutputBinding.
function Push-KeyValueOutputBinding {
    param (
        [Parameter(Mandatory=$true)]
        [string]
        $Name,

        [Parameter(Mandatory=$true)]
        [object]
        $Value,

        [switch]
        $Force
    )
    if(!$script:_OutputBindings.ContainsKey($Name) -or $Force.IsPresent) {
        $script:_OutputBindings[$Name] = $Value
    } else {
        throw "Output binding '$Name' is already set. To override the value, use -Force."
    }
}

<#
.SYNOPSIS
    Sets the value for the specified output binding.
.DESCRIPTION
    Sets the value for the specified output binding.
.EXAMPLE
    PS > Push-OutputBinding -Name res -Value "my output"
    The output binding of "res" will have the value of "my output"
.PARAMETER Name
    The name of the output binding you want to set.
.PARAMETER Value
    The value of the output binding you want to set.
.PARAMETER InputObject
    The hashtable that contains the output binding names to their respective value.
.PARAMETER Force
    (Optional) If specified, will force the value to be set for a specified output binding.
#>
function Push-OutputBinding {
    [CmdletBinding()]
    param (
        [Parameter(
            Mandatory=$true,
            ParameterSetName="NameValue",
            Position=0,
            ValueFromPipelineByPropertyName=$true)]
        [string]
        $Name,

        [Parameter(
            Mandatory=$true,
            ParameterSetName="NameValue",
            Position=1,
            ValueFromPipelineByPropertyName=$true)]
        [object]
        $Value,

        [Parameter(
            Mandatory=$true,
            ParameterSetName="InputObject",
            Position=0,
            ValueFromPipeline=$true)]
        [hashtable]
        $InputObject,

        [switch]
        $Force
    )
    process {
        switch ($PSCmdlet.ParameterSetName) {
            NameValue {
                Push-KeyValueOutputBinding -Name $Name -Value $Value -Force:$Force.IsPresent
            }
            InputObject {
                $InputObject.GetEnumerator() | ForEach-Object {
                    Push-KeyValueOutputBinding -Name $_.Name -Value $_.Value -Force:$Force.IsPresent
                }
            }
        }
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
