#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# This holds the current state of the output bindings
$script:_OutputBindings = @{}

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
            ParameterSetName="NameValue")]
        [string]
        $Name,

        [Parameter(
            Mandatory=$true,
            ParameterSetName="NameValue")]
        $Value,

        [Parameter(
            Mandatory=$true,
            ValueFromPipeline=$true,
            ParameterSetName="InputObject")]
        $InputObject,

        [switch]
        $Force
    )
    $dict = @{}
    switch ($PSCmdlet.ParameterSetName) {
        NameValue {
            $dict[$Name] = $Value
        }
        InputObject {
            $dict = $InputObject
        }
    }

    $dict.GetEnumerator() | ForEach-Object {
        if(!$script:_OutputBindings.ContainsKey($_.Name) -or $Force.IsPresent) {
            $script:_OutputBindings[$_.Name] = $_.Value
        } else {
            throw "Output binding '$($_.Name)' is already set. To override the value, use -Force."
        }
    }
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
        $Name = '*'
    )
    begin {
        $bindings = @{}
    }
    process {
        $script:_OutputBindings.GetEnumerator() | Where-Object Name -Like $Name | ForEach-Object { $null = $bindings.Add($_.Name, $_.Value) }
    }
    end {
        return $bindings
    }
}