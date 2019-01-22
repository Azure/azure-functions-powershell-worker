#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

using namespace System.Management.Automation

# This holds the current state of the output bindings.
$script:_OutputBindings = @{}
# These variables hold the ScriptBlock and CmdletInfo objects for constructing a SteppablePipeline of 'Out-String | Write-Information'.
$script:outStringCmd = $ExecutionContext.InvokeCommand.GetCommand("Microsoft.PowerShell.Utility\Out-String", [CommandTypes]::Cmdlet)
$script:writeInfoCmd = $ExecutionContext.InvokeCommand.GetCommand("Microsoft.PowerShell.Utility\Write-Information", [CommandTypes]::Cmdlet)
$script:tracingSb = { & $script:outStringCmd -Stream | & $script:writeInfoCmd -Tags "__PipelineObject__" }
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
        foreach ($entry in $script:_OutputBindings.GetEnumerator()) {
            $bindingName = $entry.Key
            $bindingValue = $entry.Value

            if ($bindingName -like $Name -and !$bindings.ContainsKey($bindingName)) {
                $bindings.Add($bindingName, $bindingValue)
            }
        }
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

    if (!$script:_OutputBindings.ContainsKey($Name) -or $Force.IsPresent) {
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
    Writes the formatted output of the pipeline object to the information stream before passing the object down to the pipeline.
.DESCRIPTION
    INTERNAL POWERSHELL WORKER USE ONLY. Writes the formatted output of the pipeline object to the information stream before passing the object down to the pipeline.
.PARAMETER InputObject
    The object from pipeline.
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
        # A micro-optimization: we use the cached 'CmdletInfo' objects to avoid command resolution every time this cmdlet is called.
        $stepPipeline = $script:tracingSb.GetSteppablePipeline([CommandOrigin]::Internal)
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
