---
external help file: Microsoft.Azure.Functions.PowerShellWorker-help.xml
Module Name: Microsoft.Azure.Functions.PowerShellWorker
online version:
schema: 2.0.0
---

# Push-OutputBinding

## SYNOPSIS
Sets the value for the specified output binding.

## SYNTAX

```
Push-OutputBinding [-Name] <String> [-Value] <Object> [-Clobber] [<CommonParameters>]
```

## DESCRIPTION
When running in the Functions runtime, this cmdlet is aware of the output bindings
defined for the function that is invoking this cmdlet.
Hence, it's able to decide
whether an output binding accepts singleton value only or a collection of values.

For example, the HTTP output binding only accepts one response object, while the
queue output binding can accept one or multiple queue messages.

With this knowledge, the 'Push-OutputBinding' cmdlet acts differently based on the
value specified for '-Name':

- If the specified name cannot be resolved to a valid output binding, then an error
  will be thrown;

- If the output binding corresponding to that name accepts a collection of values,
  then it's allowed to call 'Push-OutputBinding' with the same name repeatedly in
  the function script to push multiple values;

- If the output binding corresponding to that name only accepts a singleton value,
  then the second time calling 'Push-OutputBinding' with that name will result in
  an error, with detailed message about why it failed.

## EXAMPLES

### EXAMPLE 1
```
Push-OutputBinding -Name response -Value "output #1"
```

The output binding of "response" will have the value of "output #1"

### EXAMPLE 2
```
Push-OutputBinding -Name response -Value "output #2"
```

The output binding is 'http', which accepts a singleton value only.
So an error will be thrown from this second run.

### EXAMPLE 3
```
Push-OutputBinding -Name response -Value "output #3" -Clobber
```

The output binding is 'http', which accepts a singleton value only.
But you can use '-Clobber' to override the old value.
The output binding of "response" will now have the value of "output #3"

### EXAMPLE 4
```
Push-OutputBinding -Name outQueue -Value "output #1"
```

The output binding of "outQueue" will have the value of "output #1"

### EXAMPLE 5
```
Push-OutputBinding -Name outQueue -Value "output #2"
```

The output binding is 'queue', which accepts multiple output values.
The output binding of "outQueue" will now have a list with 2 items: "output #1", "output #2"

### EXAMPLE 6
```
Push-OutputBinding -Name outQueue -Value @("output #3", "output #4")
```

When the value is a collection, the collection will be unrolled and elements of the collection
will be added to the list.
The output binding of "outQueue" will now have a list with 4 items:
"output #1", "output #2", "output #3", "output #4".

## PARAMETERS

### -Name
The name of the output binding you want to set.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Value
The value of the output binding you want to set.

```yaml
Type: Object
Parameter Sets: (All)
Aliases:

Required: True
Position: 2
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Clobber
(Optional) If specified, will force the value to be set for a specified output binding.

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
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

## RELATED LINKS
