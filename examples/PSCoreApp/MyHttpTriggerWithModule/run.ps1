# Trigger the function by running Invoke-RestMethod:
#   Specify parameters: Invoke-RestMethod `
#                        -Uri http://localhost:7071/api/MyHttpTriggerWithModule?FromSymbol=BTC&ToSymbol=USD
#Requires -Modules @{ ModuleName="Coin"; ModuleVersion="0.2.9.44" }

# Input bindings are passed in via param block.
param($req, $TriggerMetadata)

$cmdletParameters = $req.Query

# If the cmdlet fails, we want it to throw an exception
$cmdletParameters.ErrorAction = "Stop"

try {
    # Splat the parameters that were passed in via query parameters
    $data = Get-CoinPrice @cmdletParameters
    $response = [HttpResponseContext]@{
        StatusCode = '200' # OK
        Body = $data
    }
} catch {
    $response = [HttpResponseContext]@{
        StatusCode = '400' # Bad Request
        Body = @{ Exception = $_.Exception }
    }
}

# You associate values to output bindings by calling 'Push-OutputBinding'.
Push-OutputBinding -Name res -Value $response
