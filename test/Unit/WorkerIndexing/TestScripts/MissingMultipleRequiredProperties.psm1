function MissingCosmosProps {
    [AzFunction()]
    param(
        [CosmosDBTrigger(Connection = "CosmosConn")]
        $Changes
    )
    Write-Host "This should fail - CosmosDBTrigger requires DatabaseName and ContainerName"
}
