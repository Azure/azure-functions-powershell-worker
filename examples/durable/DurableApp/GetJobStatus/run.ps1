param($name)

$randomNumber = Get-Random -Maximum 100
if ($randomNumber -lt 80) {
    "Not Completed"
}
else {
    "Completed"
}
