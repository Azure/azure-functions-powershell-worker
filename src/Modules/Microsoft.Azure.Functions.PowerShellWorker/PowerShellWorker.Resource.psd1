#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

ConvertFrom-StringData @'
###PSLOC
    OutputBindingAlreadySet=The output binding '{0}' is already set with a value. The tyep of the output binding is '{1}'. It only accepts one message/record/file per a Function invocation. To override the value, use -Clobber.
    DontPushOutputOutsideWorkerRunspace='Push-OutputBinding' should only be used in the PowerShell Language Worker's default Runspace(s). Do not use it in a custom Runsapce created during the function execution because the pushed values cannot be collected.
    BindingNameNotExist=The specified name '{0}' cannot be resolved to a valid output binding of this function.
    UnrecognizedBehavior=Unrecognized data collecting behavior '{0}'.
###PSLOC
'@
