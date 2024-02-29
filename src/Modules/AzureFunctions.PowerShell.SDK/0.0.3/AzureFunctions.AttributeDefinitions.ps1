#	
# Copyright (c) Microsoft. All rights reserved.	
# Licensed under the MIT license. See LICENSE file in the project root for full license information.	
#

class Function : Attribute {
    [string]$Name
}

class HttpTrigger : Attribute {
    [string]$AuthLevel
    [string[]]$Methods
    [string]$Route
}

class HttpOutput : Attribute {
    [string]$Name
}

class TimerTrigger : Attribute { 
    [string]$Chron
}

class EventGridTrigger : Attribute { 
    EventGridTrigger() { }
}

class DurableClient : Attribute {
    [string]$Name
}

class OrchestrationTrigger : Attribute {
}

class ActivityTrigger : Attribute {
}

class EventHubTrigger : Attribute {
    [string]$EventHubName
    [string]$ConsumerGroup
    [string]$Cardinality
    [string]$Connection
}

class EventHubOutput : Attribute {
    [string]$Name
    [string]$EventHubName
    [string]$Connection
}

class InputBinding : Attribute {
    [string]$Type
    [string]$Name
}

class OutputBinding : Attribute {
    [string]$Type
    [string]$Name
}

class AdditionalInformation : Attribute {
    [string]$BindingName
    [string]$Name
    $Value
}