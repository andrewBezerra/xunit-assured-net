<#
.SYNOPSIS
    Roda os exemplos do XUnitAssured.Camunda contra um Camunda 8.10 e lista as instâncias que
    eles criaram, com o link de cada uma no Operate.

.DESCRIPTION
    O cluster vem dos parâmetros ou das variáveis CAMUNDA_* que o SDK oficial lê:

      -Endereco   ou CAMUNDA_REST_ADDRESS (padrão http://localhost:8080)
      -Usuario    ou CAMUNDA_BASIC_AUTH_USERNAME, para Basic; a senha vem de
                  CAMUNDA_BASIC_AUTH_PASSWORD ou é pedida sem aparecer na tela
      OAuth:      CAMUNDA_CLIENT_ID, CAMUNDA_CLIENT_SECRET, CAMUNDA_OAUTH_URL e, se o provedor
                  pedir, CAMUNDA_TOKEN_AUDIENCE
      Sem nenhum dos dois: cluster sem autenticação

    O que o script define vale só durante ele: ao terminar, as variáveis voltam ao que eram.

.EXAMPLE
    ./rodar.ps1
    ./rodar.ps1 -Endereco http://camunda.minha-empresa:8080 -Usuario testes
    ./rodar.ps1 -Filtro "FullyQualifiedName~QaTests"
#>
param(
    [string]$Endereco = $(if ($env:CAMUNDA_REST_ADDRESS) { $env:CAMUNDA_REST_ADDRESS } else { "http://localhost:8080" }),
    [string]$Usuario = $env:CAMUNDA_BASIC_AUTH_USERNAME,
    [string]$Filtro
)

$ErrorActionPreference = "Stop"

$nomes = "CAMUNDA_REST_ADDRESS", "CAMUNDA_BASIC_AUTH_USERNAME", "CAMUNDA_BASIC_AUTH_PASSWORD"
$antes = @{}
foreach ($n in $nomes) { $antes[$n] = [Environment]::GetEnvironmentVariable($n) }

try {
    $Endereco = $Endereco.TrimEnd('/')
    $env:CAMUNDA_REST_ADDRESS = $Endereco

    if ($Usuario) {
        $env:CAMUNDA_BASIC_AUTH_USERNAME = $Usuario
        if (-not $env:CAMUNDA_BASIC_AUTH_PASSWORD) {
            $segura = Read-Host "Senha de $Usuario em $Endereco" -AsSecureString
            $env:CAMUNDA_BASIC_AUTH_PASSWORD = [Net.NetworkCredential]::new("", $segura).Password
        }
    }

    $inicio = (Get-Date).ToUniversalTime().AddSeconds(-5).ToString("yyyy-MM-ddTHH:mm:ssZ")

    $argumentos = @("test", "$PSScriptRoot", "--logger", "console;verbosity=detailed", "-nologo")
    if ($Filtro) { $argumentos += @("--filter", $Filtro) }
    & dotnet @argumentos
    $saida = $LASTEXITCODE

    # -----------------------------------------------------------------------------------------
    # As instâncias desta rodada, raízes e filhas, como o Operate as mostra
    # -----------------------------------------------------------------------------------------

    $cabecalho = @{}
    if ($Usuario) {
        $credencial = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${Usuario}:$env:CAMUNDA_BASIC_AUTH_PASSWORD"))
        $cabecalho.Authorization = "Basic $credencial"
    }
    elseif ($env:CAMUNDA_CLIENT_ID) {
        $pedido = @{ grant_type = "client_credentials"; client_id = $env:CAMUNDA_CLIENT_ID; client_secret = $env:CAMUNDA_CLIENT_SECRET }
        if ($env:CAMUNDA_TOKEN_AUDIENCE) { $pedido.audience = $env:CAMUNDA_TOKEN_AUDIENCE }
        $token = (Invoke-RestMethod -Method Post -Uri $env:CAMUNDA_OAUTH_URL -Body $pedido).access_token
        $cabecalho.Authorization = "Bearer $token"
    }

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
}
finally {
    foreach ($n in $nomes) { [Environment]::SetEnvironmentVariable($n, $antes[$n]) }
}
