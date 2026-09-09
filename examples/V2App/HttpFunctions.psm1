# HTTP Trigger functions using the V2 attribute-based programming model.
# No function.json files are needed — the worker indexes these automatically.

function HttpExample {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = 'Anonymous', Methods = 'GET', 'POST', Route = 'hello')]
        $Request
    )

    $name = $Request.Query['name']
    if (-not $name) {
        $name = $Request.Body.name
    }

    if ($name) {
        Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
            StatusCode = [System.Net.HttpStatusCode]::OK
            Body       = "Hello, $name!"
        })
    }
    else {
        Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
            StatusCode = [System.Net.HttpStatusCode]::BadRequest
            Body       = 'Please pass a name on the query string or in the request body.'
        })
    }
}

function GetAzureVm {
    [AzFunction(Name = 'GetAzureVm')]
    param(
        [HttpTrigger(AuthLevel = 'Function', Methods = 'GET')]
        $Request
    )

    $vmName = $Request.Query['vmName']
    $resourceGroup = $Request.Query['resourceGroup']

    if ($vmName -and $resourceGroup) {
        $vm = Get-AzVM -ResourceGroupName $resourceGroup -Name $vmName -ErrorAction SilentlyContinue
        if ($vm) {
            Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
                StatusCode = [System.Net.HttpStatusCode]::OK
                Body       = ($vm | ConvertTo-Json -Depth 5)
            })
        }
        else {
            Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
                StatusCode = [System.Net.HttpStatusCode]::NotFound
                Body       = "VM '$vmName' not found in resource group '$resourceGroup'."
            })
        }
    }
    else {
        Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
            StatusCode = [System.Net.HttpStatusCode]::BadRequest
            Body       = 'Please provide vmName and resourceGroup query parameters.'
        })
    }
}
