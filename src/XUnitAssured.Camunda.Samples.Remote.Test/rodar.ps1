<#
.SYNOPSIS
    Roda os exemplos do XUnitAssured.Camunda contra um Camunda remoto e lista as instâncias que
    eles criaram, com o link de cada uma no Operate.

.DESCRIPTION
    Sem -Senha nem $env:CAMUNDA_PASSWORD, a senha do usuário vem do servidor por SSH, sem passar
    pela tela: ~/.config/camunda/<usuario>.pass, que o "preparar.sh camunda" do repositório infra
    gera na .115.

.EXAMPLE
    ./rodar.ps1
    ./rodar.ps1 -Filtro "FullyQualifiedName~QaTests"
    ./rodar.ps1 -Endereco http://localhost:8080 -Usuario tests -Senha tests-password
#>
param(
    [string]$Endereco = "http://192.168.0.115:8080",
    [string]$Usuario = "xunit",
    [string]$Senha = $env:CAMUNDA_PASSWORD,
    [string]$Ssh = "andrewbezerra@192.168.0.115",
    [int]$PortaSsh = 2222,
    [string]$Filtro
)

$ErrorActionPreference = "Stop"

if (-not $Senha) {
    # O ssh do Windows, que fala com o ssh-agent; BatchMode falha em vez de pedir a senha da chave
    $Senha = & "$env:WINDIR\System32\OpenSSH\ssh.exe" -o BatchMode=yes -p $PortaSsh $Ssh "cat ~/.config/camunda/$Usuario.pass"
    if (-not $Senha) { throw "Não consegui ler a senha de $Usuario em $Ssh. Passe -Senha, ou rode ssh-add." }
}

$env:CAMUNDA_REST_ADDRESS = $Endereco
$env:CAMUNDA_USERNAME = $Usuario
$env:CAMUNDA_PASSWORD = $Senha

$inicio = (Get-Date).ToUniversalTime().AddSeconds(-5).ToString("yyyy-MM-ddTHH:mm:ssZ")

$argumentos = @("test", "$PSScriptRoot", "--logger", "console;verbosity=detailed", "-nologo")
if ($Filtro) { $argumentos += @("--filter", $Filtro) }
& dotnet @argumentos
$saida = $LASTEXITCODE

# ---------------------------------------------------------------------------------------------
# As instâncias desta rodada, raízes e filhas, como o Operate as mostra
# ---------------------------------------------------------------------------------------------

$credencial = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${Usuario}:$Senha"))
$cabecalho = @{ Authorization = "Basic $credencial" }

function Buscar($caminho, $corpo) {
    Invoke-RestMethod -Method Post -Uri "$Endereco/v2/$caminho" -Headers $cabecalho `
        -ContentType "application/json" -Body ($corpo | ConvertTo-Json -Depth 6)
}

$instancias = (Buscar "process-instances/search" @{
    filter = @{ startDate = @{ '$gte' = $inicio }; processDefinitionId = @{ '$in' = @("xa-pedido", "xa-envio") } }
    sort   = @(@{ field = "startDate"; order = "ASC" })
    page   = @{ limit = 100 }
}).items

Write-Host ""
Write-Host "Instâncias criadas nesta rodada ($($instancias.Count)):" -ForegroundColor Cyan

foreach ($i in $instancias | Where-Object { -not $_.parentProcessInstanceKey }) {
    $variaveis = (Buscar "variables/search" @{ filter = @{ processInstanceKey = $i.processInstanceKey }; page = @{ limit = 50 } }).items |
        Where-Object { $_.scopeKey -eq $i.processInstanceKey } |
        ForEach-Object { "$($_.name)=$($_.value)" }

    $rotulo = if ($i.processDefinitionVersionTag) { ", tag $($i.processDefinitionVersionTag)" } else { "" }
    Write-Host ""
    Write-Host "  $($i.processDefinitionId) versão $($i.processDefinitionVersion)$rotulo  $($i.state)" -ForegroundColor Yellow
    Write-Host "    $Endereco/operate/processes/$($i.processInstanceKey)"
    Write-Host "    variáveis: $($variaveis -join '  ')"

    foreach ($f in $instancias | Where-Object { $_.parentProcessInstanceKey -eq $i.processInstanceKey }) {
        Write-Host "    filha $($f.processDefinitionId) $($f.state): $Endereco/operate/processes/$($f.processInstanceKey)"
    }
}

exit $saida
