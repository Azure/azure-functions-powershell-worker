# EventHub Functions
# Groups: EventHubTriggerAndOutputObject, EventHubTriggerAndOutputString,
#         EventHubVerifyOutputObject, EventHubVerifyOutputString

function EventHubTriggerAndOutputObject {
    [AzFunction()]
    param(
        [EventHubTrigger(EventHubName = "test-input-object-ps", Connection = "EventHubConnection", Cardinality = "many")]
        $eventHubMessages,

        [EventHubOutput(EventHubName = "test-output-object-ps", Connection = "EventHubConnection")]
        $outEventHubMessage
    )

    Write-Host "PowerShell eventhub trigger function called for object message array $eventHubMessages"
    $eventHubMessages | ForEach-Object { "Processed message $_, value: $($_.value)" }
    Push-OutputBinding -Name outEventHubMessage -Value $eventHubMessages[0]
}

function EventHubTriggerAndOutputString {
    [AzFunction()]
    param(
        [EventHubTrigger(EventHubName = "test-input-string-ps", Connection = "EventHubConnection", Cardinality = "many")]
        $eventHubMessages,

        [EventHubOutput(EventHubName = "test-output-string-ps", Connection = "EventHubConnection")]
        $outEventHubMessage
    )

    Write-Host "PowerShell eventhub trigger function called for string message array $eventHubMessages"
    $eventHubMessages | ForEach-Object { "Processed message $_, value: $($_.value)" }
    Push-OutputBinding -Name outEventHubMessage -Value $eventHubMessages[0]
}

function EventHubVerifyOutputObject {
    [AzFunction()]
    param(
        [EventHubTrigger(EventHubName = "test-output-object-ps", Connection = "EventHubConnection", Cardinality = "one")]
        $eventHubMessages,

        [QueueOutput(QueueName = "test-output-object-ps", Connection = "AzureWebJobsStorage")]
        $outEventHubMessage
    )

    Write-Host "PowerShell EventHubVerifyOutputObject function called for message $eventHubMessages"
    Push-OutputBinding -Name outEventHubMessage -Value $eventHubMessages
}

function EventHubVerifyOutputString {
    [AzFunction()]
    param(
        [EventHubTrigger(EventHubName = "test-output-string-ps", Connection = "EventHubConnection", Cardinality = "one")]
        $eventHubMessages,

        [QueueOutput(QueueName = "test-output-string-ps", Connection = "AzureWebJobsStorage")]
        $outEventHubMessage
    )

    Write-Host "PowerShell EventHubVerifyOutputString function called for message $eventHubMessages"
    Push-OutputBinding -Name outEventHubMessage -Value $eventHubMessages
}
