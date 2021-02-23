param($name)

# Intentional intermittent error
$random = Get-Random -Minimum 0.0 -Maximum 1.0
if ($random -gt 0.2) {
    throw 'Nope, no luck this time...'
}

"Hello $name"
