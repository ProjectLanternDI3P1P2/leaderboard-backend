param(
    [string]$ApiUrl = 'http://localhost:8080',
    [string]$RabbitUrl = 'http://localhost:15672',
    [string]$RabbitUser = 'leaderboard',
    [string]$RabbitPassword = 'leaderboard'
)

$ErrorActionPreference = 'Stop'
$messageId = [Guid]::NewGuid().ToString()
$queueName = "smoke-$messageId"
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

    $table = Invoke-RestMethod "$ApiUrl/api/leaderboard/v1/leaderboard?page=1&pageSize=10" -TimeoutSec 15
    if ($null -eq $table.entries -or $null -eq $table.totalPlayers) { throw 'Leaderboard response mismatch.' }
    Write-Output 'PASS API league table GET HTTP 200.'

    $sql = 'SELECT COUNT(*) FROM warehouse_state WHERE id = 1;'
    $count = $sql | & docker compose exec -T postgres psql -U leaderboard -d leaderboard -tA -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0 -or ($count -join '').Trim() -ne '1') { throw "PostgreSQL verification failed: $count" }
    Write-Output 'PASS PostgreSQL warehouse state is initialized.'

    $published = Invoke-Rabbit POST "$RabbitUrl/api/exchanges/%2F/amq.default/publish" @{
        properties = @{}; routing_key = $queueName; payload = $messageId; payload_encoding = 'string'
    }
    if (-not $published.routed) { throw 'RabbitMQ did not route the test message.' }
    $messages = @(Invoke-Rabbit POST "$queueUrl/get" @{
        count = 1; ackmode = 'ack_requeue_false'; encoding = 'base64'; truncate = 50000
    })
    if ($messages.Count -ne 1) { throw 'Expected one RabbitMQ message.' }
    $bytes = [Convert]::FromBase64String($messages[0].payload)
    if ([Text.Encoding]::UTF8.GetString($bytes) -ne $messageId) {
        throw 'RabbitMQ payload mismatch.'
    }
    Write-Output 'PASS RabbitMQ management publish -> test queue -> consume: unchanged payload.'
    Write-Output 'NOTE: RabbitMQ management routing is tested independently of the event consumer.'
}
finally {
    if ($queueCreated) { $null = Invoke-Rabbit DELETE $queueUrl $null }
    Pop-Location
}
