function GenericExample {
    [AzFunction()]
    param(
        [GenericTrigger(Type = "kafkaTrigger", Connection = "KafkaConn", Properties = "brokerList=myBroker; topic=myTopic")]
        $Message
    )
    Write-Host "Kafka message: $Message"
}
