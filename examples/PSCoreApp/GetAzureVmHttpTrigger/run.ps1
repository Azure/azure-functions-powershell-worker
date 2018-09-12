# Trigger the function by running Invoke-RestMethod:
#    Get everything: Invoke-RestMethod -Uri http://localhost:7071/api/GetAzureVmHttpTrigger
#   Specify parameters: Invoke-RestMethod `
#                        -Uri http://localhost:7071/api/MyHttpTrigger?Name=testVm&ResourceGroupName=TESTRESOURCEGROUP

# Input bindings are passed in via param block.
param($req, $TriggerMetadata)

$cmdletParameters = $req.Query

# If the cmdlet fails, we want it to throw an exception
$cmdletParameters.ErrorAction = "Stop"

try {
    # Splat the parameters that were passed in via query parameters
    $vms = Get-AzureRmVM @cmdletParameters
    $response = [HttpResponseContext]@{
        StatusCode = '200' # OK
        Body = ($vms | ConvertTo-Json)
    }
} catch {
    $response = [HttpResponseContext]@{
        StatusCode = '400' # Bad Request
        Body = @{ Exception = $_.Exception }
    }
}

# You associate values to output bindings by calling 'Push-OutputBinding'.
Push-OutputBinding -Name res -Value $response
