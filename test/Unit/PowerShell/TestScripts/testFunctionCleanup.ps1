#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param ($Req)

if(!$global:foo)
{
    $global:foo = "is not set"
}

Push-OutputBinding -Name res -Value $global:foo

$global:foo = "is set"
