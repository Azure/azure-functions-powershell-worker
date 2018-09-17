#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param ($Req)

# Used for logging tests
Write-Verbose "a log"

Push-OutputBinding -Name res -Value $Req
