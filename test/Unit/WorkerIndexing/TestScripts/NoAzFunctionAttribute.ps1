# This function does NOT have [AzFunction()] and should be skipped by the indexer
function HelperFunction {
    param($value)
    return $value * 2
}
