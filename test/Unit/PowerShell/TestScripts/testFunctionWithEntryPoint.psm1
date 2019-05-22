#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Run($Req) {
    $cmdName = $MyInvocation.MyCommand.Name

    $result = "{0},{1}" -f $Req, $cmdName
    Push-OutputBinding -Name res -Value $result
}
