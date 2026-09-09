# CosmosDB Functions
# Groups: CosmosDBTriggerAndOutput

function CosmosDBTriggerAndOutput {
    [AzFunction()]
    param(
        [CosmosDBTrigger(
            DatabaseName = "ItemDb",
            ContainerName = "PartitionedItemCollectionIn",
            Connection = "CosmosDBConnection",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = $true)]
        $itemIn,

        [CosmosDBOutput(
            DatabaseName = "ItemDb",
            ContainerName = "PartitionedItemCollectionOut",
            Connection = "CosmosDBConnection")]
        $itemOut
    )

    Write-Host "PowerShell Cosmos DB trigger function executed. Received document: $itemIn"

    if ($itemIn -and $itemIn.Count -gt 0) {
        $doc = $itemIn[0]
        Write-Host "Document Id: $($doc.id)"
        $doc.Description = "testdescription"
        Push-OutputBinding -Name itemOut -Value $doc
    }
}
