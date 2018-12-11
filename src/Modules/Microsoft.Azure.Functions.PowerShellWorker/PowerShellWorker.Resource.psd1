#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

ConvertFrom-StringData @'
###PSLOC
    OutputBindingAlreadySet=Output binding '{0}' is already set. To override the value, use -Force.
    InvalidHttpOutputValue=The given value for the 'http' output binding '{0}' cannot be converted to the type 'HttpResponseContext'. The conversion failed with the following error: {1}
###PSLOC
'@
