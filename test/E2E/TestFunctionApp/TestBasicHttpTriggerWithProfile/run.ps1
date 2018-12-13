#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Input bindings are passed in via param block.
param($req)

Push-OutputBinding -Name res -Value ([HttpResponseContext]@{
    StatusCode = 202
    Body = (Get-ProfileString)
})
