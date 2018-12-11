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

# Helper private function that validates the output value and does necessary conversion.
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

    # Get the runspace where we are currently running in and then get all output bindings.
    $bindingMap = $funcMetadataType::GetOutputBindingInfo([Runspace]::DefaultRunspace.InstanceId)
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

<#
.SYNOPSIS
    Write the formatted output of the pipeline object to the information stream before passing the object down to the pipeline.
.DESCRIPTION
    Write the formatted output of the pipeline object to the information stream before passing the object down to the pipeline.
    This function is for the PowerShell worker to use only, to trace objects written into the pipeline in a streaming way.
.PARAMETER InputObject
    The object from pipeline.
.PARAMETER WriteToInformationChannel
    Make the command write the pipeline object to the information stream.
#>
function Trace-PipelineObject {

    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [object]
        $InputObject
    )

    <#
        This function behaves like 'Tee-Object'.
        An input pipeline object is first pushed through a steppable pipeline that consists of 'Out-String | Write-Information -Tags "__PipelineObject__"',
        and then it's written out back to the pipeline without change. In this approach, we can intercept and trace the pipeline objects in a streaming way
        and keep the objects in pipeline at the same time.
    #>

    Begin {
        $outStringCmd = $ExecutionContext.InvokeCommand.GetCommand("Microsoft.PowerShell.Utility\Out-String", [CommandTypes]::Cmdlet)
        $writeInfoCmd = $ExecutionContext.InvokeCommand.GetCommand("Microsoft.PowerShell.Utility\Write-Information", [CommandTypes]::Cmdlet)

        $stepPipeline = { & $outStringCmd -Stream | & $writeInfoCmd -Tags "__PipelineObject__" }.GetSteppablePipeline([CommandOrigin]::Internal)
        $stepPipeline.Begin($PSCmdlet)
    }

    Process {
        $stepPipeline.Process($InputObject)
        $InputObject
    }

    End {
        $stepPipeline.End()
    }
}
