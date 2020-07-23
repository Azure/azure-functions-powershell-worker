param($name)

$randomNumber = Get-Random -Maximum 100
if ($randomNumber -lt 60) {
    "Not Completed"
}
else {
    "Completed"
}
