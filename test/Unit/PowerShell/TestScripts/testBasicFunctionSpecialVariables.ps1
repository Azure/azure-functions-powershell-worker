#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param ($Req)

# Used for logging tests
Write-Verbose "a log"
$cmdName = $MyInvocation.MyCommand.Name

$result = "{0},{1},{2}" -f $PSScriptRoot, $PSCommandPath, $cmdName
Push-OutputBinding -Name res -Value $result
