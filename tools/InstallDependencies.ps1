#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

$dependencies = Import-PowerShellDataFile "$PSScriptRoot/../src/requirements.psd1"

foreach ($key in $dependencies.Keys) {
    $params = @{ Name = $key }

    if ($dependencies[$key].Version -ne 'latest') {
        # Save-Module doesn't have -Version so we have to specify Min and Max
        $params.MinimumVersion = $dependencies[$key].Version
        $params.MaximumVersion = $dependencies[$key].Version
    }
    
    if($dependencies[$key].Target -eq 'CurrentUser') {
        $params.Scope = $dependencies[$key].Target
        Install-Module @params
    } else {
        $params.Path = $dependencies[$key].Target
        if (Test-Path "$($params.Path)/$key") {
            Write-Host "'$key' - Module already installed"
        } else {
            Save-Module @params
        }
    }
}
