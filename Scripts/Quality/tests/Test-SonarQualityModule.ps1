[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..\SonarQuality.psm1') -Force -DisableNameChecking

$passed = 0
function Assert-Equal {
    param([object]$Actual, [object]$Expected, [string]$Name)
    if ($Actual -cne $Expected) { throw "$Name expected '$Expected', got '$Actual'." }
    $script:passed++
}
function Assert-Throws {
    param([scriptblock]$Action, [string]$Name, [string]$ExpectedMessage)
    try { & $Action; throw "$Name did not fail." }
    catch {
        if ($_.Exception.Message -eq "$Name did not fail.") { throw }
        if ($ExpectedMessage -and -not $_.Exception.Message.Contains($ExpectedMessage)) { throw }
    }
    $script:passed++
}

$secret = 'module-test-token-sentinel'
$arguments = @(ConvertTo-QualityDisplayArguments -Arguments @('/k:project', "/d:sonar.token=$secret", 'Authorization: Bearer value123'))
Assert-Equal $arguments[0] '/k:project' 'safe scanner argument'
if (($arguments -join ' ') -match [regex]::Escape($secret) -or ($arguments -join ' ') -match 'value123') { throw 'Argument display leaked a credential.' }
$redacted = Protect-QualityText -Text "before $secret after" -Secrets @($secret)
if ($redacted.Contains($secret) -or -not $redacted.Contains('[REDACTED]')) { throw 'Protect-QualityText did not redact the supplied secret.' }
$passed++

Assert-Equal (ConvertTo-RepositoryIdentity 'https://user:password@example.com/Owner/Repo.git') 'example.com/Owner/Repo' 'origin normalization'
Assert-Throws { ConvertTo-RepositoryIdentity 'file:///tmp/repo' } 'unsupported origin scheme'
Assert-Equal (New-QualityAuthHeader -Token 'token-value').Authorization 'Bearer token-value' 'bearer authentication'
Assert-Throws { New-QualityAuthHeader -User admin } 'incomplete basic authentication'
Assert-Equal (Get-QualityTokenProjectKey -Token ([pscustomobject]@{ projectKey = 'legacy-project-key' })) 'legacy-project-key' 'legacy token project key'
Assert-Equal (Get-QualityTokenProjectKey -Token ([pscustomobject]@{ project = [pscustomobject]@{ key = 'current-project-key'; name = 'Current project' } })) 'current-project-key' 'current token project key'
Assert-Equal (Get-QualityTokenProjectKey -Token ([pscustomobject]@{ project = $null })) $null 'missing token project key'

# Server ownership: a lease marks a server an audit started, so a killed audit's server is adopted and stopped later.
$fresh = Resolve-QualityServerOwnership -WasRunning $false -LeasePresent $false -KeepRequested $false
Assert-Equal "$($fresh.AdoptedLeftover)/$($fresh.WriteLease)/$($fresh.RemoveLease)" 'False/True/False' 'fresh start writes a lease'
$leftover = Resolve-QualityServerOwnership -WasRunning $true -LeasePresent $true -KeepRequested $false
Assert-Equal "$($leftover.AdoptedLeftover)/$($leftover.WriteLease)/$($leftover.RemoveLease)" 'True/False/False' 'killed audit stack is adopted with its original lease'
$manual = Resolve-QualityServerOwnership -WasRunning $true -LeasePresent $false -KeepRequested $false
Assert-Equal "$($manual.AdoptedLeftover)/$($manual.WriteLease)/$($manual.RemoveLease)" 'False/False/False' 'manually started server is left alone'
$kept = Resolve-QualityServerOwnership -WasRunning $true -LeasePresent $true -KeepRequested $true
Assert-Equal "$($kept.AdoptedLeftover)/$($kept.WriteLease)/$($kept.RemoveLease)" 'False/False/True' 'keep request releases ownership without adopting the stack'
$keptFresh = Resolve-QualityServerOwnership -WasRunning $false -LeasePresent $false -KeepRequested $true
Assert-Equal "$($keptFresh.WriteLease)/$($keptFresh.RemoveLease)" 'False/False' 'kept fresh server has no lease'
$stale = Resolve-QualityServerOwnership -WasRunning $false -LeasePresent $true -KeepRequested $false
Assert-Equal "$($stale.AdoptedLeftover)/$($stale.WriteLease)" 'False/True' 'stale lease without a server is replaced'

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) "quality-module-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tempRoot | Out-Null
try {
    $safePath = Join-Path $tempRoot 'reports/summary.json'
    Assert-Equal (Test-QualityPathWithinRoot -Path $safePath -Root $tempRoot) $true 'path containment'
    Assert-Equal (Test-QualityPathWithinRoot -Path (Join-Path $tempRoot '../outside.json') -Root $tempRoot) $false 'path escape rejection'
    Assert-Throws { Assert-QualitySafeChildPath -Path (Join-Path $tempRoot 'NUL.txt') -Root $tempRoot } 'reserved path rejection'
    Assert-Throws {
        Invoke-QualityApi -BaseUrl 'https://sonar.example.com' -Path 'api/system/status' -Headers @{}
    } 'remote Sonar endpoint rejection'

    $lockContext = [pscustomobject]@{ BaseDirectory = $tempRoot; LockPath = Join-Path $tempRoot 'audit.lock' }
    $lock = Enter-QualityLock -Context $lockContext -Operation 'test' -RunId 'module-check' -TimeoutSeconds 1
    try {
        Assert-Throws { Enter-QualityLock -Context $lockContext -Operation 'test' -RunId 'contender' -TimeoutSeconds 1 } 'exclusive lock'
    }
    finally { Exit-QualityLock -Lock $lock }
    $released = Enter-QualityLock -Context $lockContext -Operation 'test' -RunId 'after-release' -TimeoutSeconds 1
    Exit-QualityLock -Lock $released

    if ($IsWindows) {
        $secretContext = [pscustomobject]@{ SecretsDirectory = Join-Path $tempRoot 'encrypted-secrets' }
        Set-QualitySecret -Context $secretContext -Name 'module-test-only' -Value $secret
        $secretPath = Get-QualitySecretPath -Context $secretContext -Name 'module-test-only'
        if ([IO.File]::ReadAllText($secretPath).Contains($secret)) { throw 'DPAPI secret file contains plaintext.' }
        Assert-Equal (Get-QualitySecret -Context $secretContext -Name 'module-test-only') $secret 'DPAPI round trip'
        Remove-QualitySecret -Context $secretContext -Name 'module-test-only'
    }
    # Exercise the real lifecycle entry point with a fake Compose process and private settings.
    $fixtureRoot = Join-Path $tempRoot 'server-lifecycle'
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    $fixtureServer = Join-Path $fixtureRoot 'SonarQubeServer.ps1'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../SonarQubeServer.ps1') -Destination $fixtureServer
    $fixtureModule = Join-Path $fixtureRoot 'SonarQuality.psm1'
    Set-Content -LiteralPath $fixtureModule -Value @'
function Get-QualityContext {
    [pscustomobject]@{
        RepositoryIdentity = 'github.com/quality-test/fixture'
        ComposeProject = 'sonarqube-quality-test-01234567'
        ComposeNetwork = 'fixture-network'
        ComposePath = 'fixture-compose.yaml'
        BaseDirectory = $PSScriptRoot
        ServerLeasePath = Join-Path $PSScriptRoot 'server-lease.json'
        DockerExecutable = 'mock-docker'
        IdentityHash = 'fixture-hash'
        HostPort = 9000
    }
}
function Enter-QualityLock { 'fixture-lock' }
function Exit-QualityLock {}
function Resolve-QualityHostPort {}
function Save-QualityPrivateSettings {}
function Get-QualitySecret { 'fixture-password' }
function Test-QualityPortAvailable { $true }
function Protect-QualityText { param($Text) $Text }
function Invoke-QualityProcess {
    param([string[]]$ArgumentList)
    $result = [pscustomobject]@{ ExitCode = 0; TimedOut = $false; Stdout = ''; Stderr = '' }
    if ($ArgumentList[0] -eq 'info') {
        $result.Stdout = 'linux'
    }
    elseif (($ArgumentList -contains 'up') -or (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'fail-stop'))) {
        $result.ExitCode = 1
        $result.Stderr = 'fixture Compose failure'
    }
    return $result
}
'@
    $mock = Import-Module -Name $fixtureModule -PassThru -DisableNameChecking
    $fixtureContext = & $mock { Get-QualityContext }
    $leasePath = $fixtureContext.ServerLeasePath
    [IO.File]::WriteAllText($leasePath, '{}')
    Assert-Throws { & $fixtureServer -Start -RepoRoot $fixtureRoot } 'interrupted explicit start' 'fixture Compose failure'
    Assert-Equal ([IO.File]::Exists($leasePath)) $false 'explicit start transfers ownership before Compose up'

    [IO.File]::WriteAllText($leasePath, '{}')
    Assert-Throws { & $fixtureServer -Start -RepoRoot $fixtureRoot -Context $fixtureContext } 'interrupted delegated start' 'fixture Compose failure'
    Assert-Equal ([IO.File]::Exists($leasePath)) $true 'an audit start retains its lease'

    $failStopPath = Join-Path $fixtureRoot 'fail-stop'
    [IO.File]::WriteAllText($failStopPath, '')
    Assert-Throws { & $fixtureServer -Stop -RepoRoot $fixtureRoot } 'failed explicit stop' 'fixture Compose failure'
    Assert-Equal ([IO.File]::Exists($leasePath)) $true 'failed stop retains the lease for cleanup'
    [IO.File]::Delete($failStopPath)
    & $fixtureServer -Stop -RepoRoot $fixtureRoot
    Assert-Equal ([IO.File]::Exists($leasePath)) $false 'successful explicit stop clears the lease'
}
finally { Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host "Test-SonarQualityModule: $passed assertions passed."
