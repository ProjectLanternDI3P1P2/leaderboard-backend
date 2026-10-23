param(
    [string] $OutputDirectory = (Join-Path ([IO.Path]::GetTempPath()) ("leaderboard-contracts-" + [Guid]::NewGuid().ToString('N'))),
    [string] $ExpectedTag
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repository 'Leaderboard.Contracts/Leaderboard.Contracts.csproj'
[xml] $projectXml = Get-Content -LiteralPath $project -Raw
$version = [string] $projectXml.Project.PropertyGroup.Version
if ($ExpectedTag -and $ExpectedTag -cne "leaderboard-contracts-v$version") {
    throw "Tag '$ExpectedTag' does not match package version '$version'."
}
if (Test-Path -LiteralPath $OutputDirectory) {
    throw 'Use a new output directory to avoid testing or publishing a stale package.'
}
$output = (New-Item -ItemType Directory -Path $OutputDirectory).FullName

dotnet pack $project --configuration Release --output $output
if ($LASTEXITCODE -ne 0) { throw 'Contract packaging failed.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$package = Join-Path $output "Leaderboard.Contracts.$version.nupkg"
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    foreach ($entry in @('README.md', 'lib/net10.0/Leaderboard.Contracts.dll', 'proto/collection_events_v1.proto', 'proto/leaderboard_events_v1.proto')) {
        if ($null -eq $archive.GetEntry($entry)) { throw "Missing package entry: $entry" }
    }
    $reader = [IO.StreamReader]::new($archive.GetEntry('Leaderboard.Contracts.nuspec').Open())
    try { [xml] $spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $metadata = $spec.package.metadata
    if ($metadata.id -cne 'Leaderboard.Contracts' -or $metadata.version -cne $version) {
        throw 'Unexpected package identity.'
    }
    if ($metadata.repository.url -cne 'https://github.com/ProjectLanternDI3P1P2/leaderboard-backend') {
        throw 'The package must be linked to its owning repository.'
    }
    $dependencies = @($metadata.dependencies.group.dependency)
    if ($dependencies.Count -ne 1 -or $dependencies[0].id -cne 'Google.Protobuf') {
        throw 'Contracts must depend only on Google.Protobuf, without service or build-tool dependencies.'
    }
} finally {
    $archive.Dispose()
}

# A separate project and package cache prove consumption of the nupkg, not a ProjectReference.
$consumer = (New-Item -ItemType Directory -Path (Join-Path $output 'consumer')).FullName
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Consumer/Consumer.csproj'), (Join-Path $PSScriptRoot 'Consumer/Program.cs') -Destination $consumer
Copy-Item -LiteralPath (Join-Path $repository 'docs/fixtures/us-collecte-01') -Destination (Join-Path $consumer 'Fixtures') -Recurse
$source = [Security.SecurityElement]::Escape($output)
$configuration = @"
<configuration>
  <packageSources>
    <clear />
    <add key="local-contracts" value="$source" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-contracts"><package pattern="Leaderboard.Contracts" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@
$configFile = Join-Path $consumer 'NuGet.config'
[IO.File]::WriteAllText($configFile, $configuration, [Text.UTF8Encoding]::new($false))
$consumerProject = Join-Path $consumer 'Consumer.csproj'
$cache = Join-Path $output 'packages'
dotnet restore $consumerProject --configfile $configFile --packages $cache "-p:ContractsVersion=$version"
if ($LASTEXITCODE -ne 0) { throw 'Standalone package restore failed.' }
dotnet run --project $consumerProject --configuration Release --no-restore "-p:ContractsVersion=$version" "-p:RestorePackagesPath=$cache"
if ($LASTEXITCODE -ne 0) { throw 'Standalone package consumer failed.' }
Write-Output "Verified package: $package"
