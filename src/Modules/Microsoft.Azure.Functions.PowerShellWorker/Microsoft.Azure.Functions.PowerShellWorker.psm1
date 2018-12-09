#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

using namespace System.Management.Automation
using namespace System.Management.Automation.Runspaces
using namespace Microsoft.Azure.Functions.PowerShellWorker

# This holds the current state of the output bindings.
$script:_OutputBindings = @{}
# This loads the resource strings.
Import-LocalizedData LocalizedData -FileName PowerShellWorker.Resource.psd1

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

# Helper private function that validates the output value and do necessary conversion.
function Convert-OutputBindingValue {
    param(
        [Parameter(Mandatory=$true)]
        [string]
        $Name,

        [Parameter(Mandatory=$true)]
        [object]
        $Value
    )

    # Check if we can get the binding metadata of the current running function.
    $funcMetadataType = "FunctionMetadata" -as [type]
    if ($null -eq $funcMetadataType) {
        return $Value
    }

    # Get the runspace where we are currently running in.
    $runspace = [Runspace]::DefaultRunspace
    # Get all output bindings.
    $bindingMap = $funcMetadataType::GetOutputBindingInfo($runspace.InstanceId)
    if ($null -eq $bindingMap) {
        return $Value
    }

    # Get the binding information of given output binding name.
    $bindingInfo = $bindingMap[$Name]
    if ($bindingInfo.Type -ne "http") {
        return $Value
    }

    # Nothing to do if the value is already a HttpResponseContext object.
    if ($Value -as [HttpResponseContext]) {
        return $Value
    }

    try {
        return [LanguagePrimitives]::ConvertTo($Value, [HttpResponseContext])
    } catch [PSInvalidCastException] {
        $conversionMsg = $_.Exception.Message
        $errorMsg = $LocalizedData.InvalidHttpOutputValue -f $Name, $conversionMsg
        throw $errorMsg
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

    if (!$script:_OutputBindings.ContainsKey($Name) -or $Force.IsPresent) {
        $Value = Convert-OutputBindingValue -Name $Name -Value $Value
        $script:_OutputBindings[$Name] = $Value
    } else {
        $errorMsg = $LocalizedData.OutputBindingAlreadySet -f $Name
        throw $errorMsg
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
