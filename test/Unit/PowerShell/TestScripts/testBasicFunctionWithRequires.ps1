#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

#Requires -Modules ThreadJob

param ($req)

$module = Get-Module ThreadJob
$cmdName = $MyInvocation.MyCommand.Name

$result = "{0},{1},{2}" -f $req, $module.Name, $cmdName
Push-OutputBinding -Name res -Value $result
