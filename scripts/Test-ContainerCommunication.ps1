param(
    [string]$ApiUrl = 'http://localhost:8080',
    [string]$RabbitUrl = 'http://localhost:15672',
    [string]$RabbitUser = 'leaderboard',
    [string]$RabbitPassword = 'leaderboard'
)

$ErrorActionPreference = 'Stop'
$playerId = [Guid]::NewGuid().ToString()
$queueName = "smoke-$playerId"
$basic = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${RabbitUser}:${RabbitPassword}"))
$headers = @{ Authorization = "Basic $basic" }
$queueUrl = "$RabbitUrl/api/queues/%2F/$queueName"
$queueCreated = $false

function Invoke-Rabbit([string]$Method, [string]$Url, $Body) {
    $request = @{ Method = $Method; Uri = $Url; Headers = $headers; TimeoutSec = 15 }
    if ($null -ne $Body) {
        $request.ContentType = 'application/json'
        $request.Body = ConvertTo-Json -InputObject $Body -Depth 10
    }
    Invoke-RestMethod @request
}

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $health = Invoke-WebRequest "$ApiUrl/health/live" -UseBasicParsing -TimeoutSec 15
    if ($health.StatusCode -ne 200) { throw 'API health failed.' }
    Write-Output 'PASS API health HTTP 200 (liveness only).'

    $null = Invoke-Rabbit PUT $queueUrl @{ durable = $true; auto_delete = $false; arguments = @{} }
    $queueCreated = $true

    $body = @{ id = $playerId; name = 'ContainerSmokeTest'; attack = 10; health = 100; maxHealth = 100 }
    $created = Invoke-WebRequest "$ApiUrl/api/v1/players" -Method POST -ContentType 'application/json' `
        -Body (ConvertTo-Json $body) -UseBasicParsing -TimeoutSec 30
    if ($created.StatusCode -ne 201) { throw "Expected HTTP 201, got $($created.StatusCode)." }
    $player = Invoke-RestMethod "$ApiUrl/api/v1/players/$playerId" -TimeoutSec 15
    if ($player.id -ne $playerId -or $player.name -ne $body.name) { throw 'REST read-back mismatch.' }
    Write-Output "PASS API create HTTP 201 and GET read-back: $playerId"

    $sql = "SELECT COUNT(*) FROM ""Players"" WHERE ""PlayerId"" = '$playerId' AND ""PlayerName"" = 'ContainerSmokeTest';"
    $count = $sql | & docker compose exec -T postgres psql -U leaderboard -d leaderboard -tA -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0 -or ($count -join '').Trim() -ne '1') { throw "PostgreSQL verification failed: $count" }
    Write-Output 'PASS API -> PostgreSQL: exactly one matching persisted player.'

    $published = Invoke-Rabbit POST "$RabbitUrl/api/exchanges/%2F/amq.default/publish" @{
        properties = @{}; routing_key = $queueName; payload = $playerId; payload_encoding = 'string'
    }
    if (-not $published.routed) { throw 'RabbitMQ did not route the test message.' }
    $messages = @(Invoke-Rabbit POST "$queueUrl/get" @{
        count = 1; ackmode = 'ack_requeue_false'; encoding = 'base64'; truncate = 50000
    })
    if ($messages.Count -ne 1) { throw 'Expected one RabbitMQ message.' }
    $bytes = [Convert]::FromBase64String($messages[0].payload)
    if ([Text.Encoding]::UTF8.GetString($bytes) -ne $playerId) {
        throw 'RabbitMQ payload mismatch.'
    }
    Write-Output 'PASS RabbitMQ management publish -> test queue -> consume: unchanged payload.'
    Write-Output 'NOTE: RabbitMQ is tested independently; no application publisher/consumer is implemented.'
    Write-Output "NOTE: test player retained in the local database: $playerId"
}
finally {
    if ($queueCreated) { $null = Invoke-Rabbit DELETE $queueUrl $null }
    Pop-Location
}
