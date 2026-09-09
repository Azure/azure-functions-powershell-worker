param ($Req)

# Produce multiple pipeline objects — this is a pipeline leak
"first item"
"second item"
"third item"

Push-OutputBinding -Name res -Value "done"
