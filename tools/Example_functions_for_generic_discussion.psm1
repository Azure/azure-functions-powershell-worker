function ExecuteHttpTrigger() {
    [Function('FunctionName')]
    [HttpOutput('Response')]
    param(
        [HttpTrigger('anonymous', ('GET'), 'execute-route')]
        [DurableClient('starter')]
        $Request,
        $TriggerMetadata
    )
    # do stuff
}

function ExecuteHttpTrigger() {
    [Function('FunctionName')]
    param(
        [GenericBinding(Type='httpTrigger', Name='Request', Direction='in')]
        [AdditionalInformation(BindingName='Request', Name='authLevel', Value='anonymous')]
        [AdditionalInformation('Request', 'methods', ('GET'))]

        [GenericBinding('http', 'Response', 'out')]

        [GenericBinding('durableClient', 'starter', 'in')]
        $Request,
        [GenericBinding('blob', 'Blob', 'in')]
        [AdditionalInformation('Blob', 'infoName', 'value')]
        $Blob,
        $TriggerMetadata
    )
    # do stuff
}


function ExecuteHttpTrigger() {
    [Function('FunctionName')]
    [OutputBinding('http', 'Response')]
    param(
        [InputBinding(Type='httpTrigger')]
        [AdditionalInformation(Name='authLevel', Value='anonymous')]
        [AdditionalInformation('methods', ('GET'))]
        $Request,
        [InputBinding('blob')]
        [AdditionalInformation('Blob', 'infoName', 'value')]
        $Blob,
        [InputBinding('durableClient', 'starter')]
        $TriggerMetadata
    )
    # do stuff
}
