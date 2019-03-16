#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

using namespace System.Management.Automation
using namespace System.Management.Automation.Runspaces
using namespace Microsoft.Azure.Functions.PowerShellWorker

# This holds the current state of the output bindings.
$script:_OutputBindings = @{}
$script:_FuncMetadataType = "FunctionMetadata" -as [type]
$script:_RunningInPSWorker = $null -ne $script:_FuncMetadataType
# These variables hold the ScriptBlock and CmdletInfo objects for constructing a SteppablePipeline of 'Out-String | Write-Information'.
$script:outStringCmd = $ExecutionContext.InvokeCommand.GetCommand("Microsoft.PowerShell.Utility\Out-String", [CommandTypes]::Cmdlet)
$script:writeInfoCmd = $ExecutionContext.InvokeCommand.GetCommand("Microsoft.PowerShell.Utility\Write-Information", [CommandTypes]::Cmdlet)
$script:tracingSb = { & $script:outStringCmd -Stream | & $script:writeInfoCmd -Tags "__PipelineObject__" }
# This loads the resource strings.
Import-LocalizedData LocalizedData -FileName PowerShellWorker.Resource.psd1

# Enum that defines different behaviors when collecting output data
enum DataCollectingBehavior {
    Scalar
    Collection
}

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

# Helper private function that resolve the name to the corresponding binding information.
function Get-BindingInfo
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    if ($script:_RunningInPSWorker)
    {
        $instanceId = [Runspace]::DefaultRunspace.InstanceId
        $bindingMap = $script:_FuncMetadataType::GetOutputBindingInfo($instanceId)

        # If the instance id doesn't get us back a binding map, then we are not running in one of the PS worker's default Runspace(s).
        # This could happen when a custom Runspace is created in the function script, and 'Push-OutputBinding' is called in that Runspace.
        if ($null -eq $bindingMap)
        {
            throw $LocalizedData.DontPushOutputOutsideWorkerRunspace
        }

        $bindingInfo = $bindingMap[$Name]
        if ($null -eq $bindingInfo)
        {
            $errorMsg = $LocalizedData.BindingNameNotExist -f $Name
            throw $errorMsg
        }

        return $bindingInfo
    }
}

# Helper private function that maps an output binding to a data collecting behavior.
function Get-DataCollectingBehavior
{
    param($BindingInfo)

    # binding info not available
    if ($null -eq $BindingInfo)
    {
        return [DataCollectingBehavior]::Scalar
    }

    switch ($BindingInfo.Type)
    {
        "http" { return [DataCollectingBehavior]::Scalar }
        "blob" { return [DataCollectingBehavior]::Scalar }

        "sendGrid" { return [DataCollectingBehavior]::Scalar }
        "onedrive" { return [DataCollectingBehavior]::Scalar }
        "outlook"  { return [DataCollectingBehavior]::Scalar }
        "notificationHub" { return [DataCollectingBehavior]::Scalar }

        "excel"    { return [DataCollectingBehavior]::Collection }
        "table"    { return [DataCollectingBehavior]::Collection }
        "queue"    { return [DataCollectingBehavior]::Collection }
        "eventHub" { return [DataCollectingBehavior]::Collection }
        "documentDB"  { return [DataCollectingBehavior]::Collection }
        "mobileTable" { return [DataCollectingBehavior]::Collection }
        "serviceBus"  { return [DataCollectingBehavior]::Collection }
        "signalR"   { return [DataCollectingBehavior]::Collection }
        "twilioSms" { return [DataCollectingBehavior]::Collection }
        "graphWebhookSubscription" { return [DataCollectingBehavior]::Collection }

        # Be conservative on new output bindings
        default { return [DataCollectingBehavior]::Scalar }
    }
}

<#
.SYNOPSIS
    Combine the new data with the existing data for a output binding with 'Collection' behavior.
    Here is what this command do:
    - when there is no existing data
      - if the new data is considered enumerable by PowerShell,
        then all its elements get added to a List<object>, and that list is returned.
      - otherwise, the new data is returned intact.

    - when there is existing data
      - if the existing data is a scalar, then a List<object> is created and the existing data
        is added to the list.
      - otherwise, the existing data is already a List<object>
      - Then, depending on whether the new data is enumerable or not, its elements or itself will also be added to the list.
      - That list is returned.
#>
function Merge-Collection
{
    param($OldData, $NewData)

    $isNewDataEnumerable = [LanguagePrimitives]::IsObjectEnumerable($NewData)

    if ($null -eq $OldData -and -not $isNewDataEnumerable)
    {
        return $NewData
    }

    $list = $OldData -as [System.Collections.Generic.List[object]]
    if ($null -eq $list)
    {
        $list = [System.Collections.Generic.List[object]]::new()
        if ($null -ne $OldData)
        {
            $list.Add($OldData)
        }
    }

    if ($isNewDataEnumerable)
    {
        foreach ($item in $NewData)
        {
            $list.Add($item)
        }
    }
    else
    {
        $list.Add($NewData)
    }

    return ,$list
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
function Push-OutputBinding
{
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true, Position = 0)]
        [string] $Name,

        [Parameter(Mandatory = $true, Position = 1, ValueFromPipeline = $true)]
        [object] $Value,

        [switch] $Clobber
    )

    Begin
    {
        $bindingInfo = Get-BindingInfo -Name $Name
        $behavior = Get-DataCollectingBehavior -BindingInfo $bindingInfo
    }

    process
    {
        $bindingType = "UnKnown"
        if ($null -ne $bindingInfo)
        {
            $bindingType = $bindingInfo.Type
        }

        if (-not $script:_OutputBindings.ContainsKey($Name))
        {
            switch ($behavior)
            {
                ([DataCollectingBehavior]::Scalar)
                {
                    $script:_OutputBindings[$Name] = $Value
                    return
                }

                ([DataCollectingBehavior]::Collection)
                {
                    $newValue = Merge-Collection -OldData $null -NewData $Value
                    $script:_OutputBindings[$Name] = $newValue
                    return
                }

                default
                {
                    $errorMsg = $LocalizedData.UnrecognizedBehavior -f $behavior
                    throw $errorMsg
                }
            }
        }

        ## Key already exists in _OutputBindings
        switch ($behavior)
        {
            ([DataCollectingBehavior]::Scalar)
            {
                if ($Clobber.IsPresent)
                {
                    $script:_OutputBindings[$Name] = $Value
                    return
                }
                else
                {
                    $errorMsg = $LocalizedData.OutputBindingAlreadySet -f $Name, $bindingType
                    throw $errorMsg
                }
            }

            ([DataCollectingBehavior]::Collection)
            {
                if ($Clobber.IsPresent)
                {
                    $newValue = Merge-Collection -OldData $null -NewData $Value
                }
                else
                {
                    $oldValue = $script:_OutputBindings[$Name]
                    $newValue = Merge-Collection -OldData $oldValue -NewData $Value
                }

                $script:_OutputBindings[$Name] = $newValue
                return
            }

            default
            {
                $errorMsg = $LocalizedData.UnrecognizedBehavior -f $behavior
                throw $errorMsg
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
