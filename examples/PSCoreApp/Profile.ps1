# This script will be run on every COLD START of the Function App
# You can define helper functions, run commands, or specify environment variables
# NOTE: any variables defined that are not environment variables will get reset after the first execution

# Example usecases for a profile.ps1:

# Authenticating with Azure PowerShell using MSI
# $tokenAuthURI = $env:MSI_ENDPOINT + "?resource=https://management.azure.com&api-version=2017-09-01"
# $tokenResponse = Invoke-RestMethod -Method Get -Headers @{"Secret"="$env:MSI_SECRET"} -Uri $tokenAuthURI
# Connect-AzAccount -AccessToken $tokenResponse.access_token -AccountId $env:WEBSITE_SITE_NAME

# Enabling legacy AzureRm alias in Azure PowerShell
# Enable-AzureRmAlias

# Defining a function that you can refernce in any of your PowerShell functions:
# function Get-MyData {
#     @{
#         Foo = 5
#     }
# }
