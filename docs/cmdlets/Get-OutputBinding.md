---
external help file: Microsoft.Azure.Functions.PowerShellWorker-help.xml
Module Name: Microsoft.Azure.Functions.PowerShellWorker
online version:
schema: 2.0.0
---

# Get-OutputBinding

## SYNOPSIS
Gets the hashtable of the output bindings set so far.

## SYNTAX

```
Get-OutputBinding [[-Name] <String[]>] [-Purge] [<CommonParameters>]
```

## DESCRIPTION
Gets the hashtable of the output bindings set so far.

## EXAMPLES

### EXAMPLE 1
```
Get-OutputBinding
```

Gets the hashtable of all the output bindings set so far.

### EXAMPLE 2
```
Get-OutputBinding -Name res
```

Gets the hashtable of specific output binding.

### EXAMPLE 3
```
Get-OutputBinding -Name r*
```

Gets the hashtable of output bindings that match the wildcard.

## PARAMETERS

### -Name
The name of the output binding you want to get.
Supports wildcards.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 1
Default value: *
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: True
```

### -Purge
Clear all stored output binding values.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see about_CommonParameters (http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### The hashtable of binding names to their respective value.
## NOTES

## RELATED LINKS
