param(
  [switch]$SkipCertificate
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot '.env'
$examplePath = Join-Path $repoRoot '.env.example'

if (-not (Test-Path $envPath)) {
  Copy-Item -Path $examplePath -Destination $envPath
  Write-Host "Created .env from .env.example"
}
else {
  Write-Host ".env already exists"
  $existingKeys = @{}
  Get-Content -Path $envPath | ForEach-Object {
    if ($_ -match '^\s*([^#][^=]+)=') {
      $existingKeys[$matches[1].Trim()] = $true
    }
  }

  $missingLines = @()
  Get-Content -Path $examplePath | ForEach-Object {
    if ($_ -match '^\s*([^#][^=]+)=') {
      $key = $matches[1].Trim()
      if (-not $existingKeys.ContainsKey($key)) {
        $missingLines += $_
      }
    }
  }

  if ($missingLines.Count -gt 0) {
    Add-Content -Path $envPath -Value ""
    Add-Content -Path $envPath -Value "# Added by docker/setup-local.ps1 from .env.example"
    Add-Content -Path $envPath -Value $missingLines
    Write-Host "Added missing .env key(s): $($missingLines.Count)"
  }
}

function New-LocalPassword {
  # Letters and digits only: safe for raw .env parsing and within the account password policy.
  $sets = @('ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz', '0123456789')
  $all = -join $sets
  while ($true) {
    $chars = for ($i = 0; $i -lt 24; $i++) { $all[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($all.Length)] }
    $password = -join $chars
    if ($password -cmatch '[A-Z]' -and $password -cmatch '[a-z]' -and $password -match '[0-9]') {
      return $password
    }
  }
}

function Set-LocalSeed([string]$Prefix, [string]$Email) {
  $lines = [System.Collections.Generic.List[string]]::new()
  foreach ($line in Get-Content -Path $envPath) { $lines.Add($line) }
  $values = @{}
  foreach ($line in $lines) {
    $separator = $line.IndexOf('=')
    if ($separator -gt 0 -and -not $line.TrimStart().StartsWith('#')) {
      $values[$line.Substring(0, $separator).Trim()] = $line.Substring($separator + 1)
    }
  }

  # A seed someone already configured is left exactly as it is.
  if (-not [string]::IsNullOrEmpty($values["${Prefix}__Email"]) -or -not [string]::IsNullOrEmpty($values["${Prefix}__Password"])) {
    return
  }

  $wanted = [ordered]@{
    "${Prefix}__Enabled" = 'true'
    "${Prefix}__Email" = $Email
    "${Prefix}__Password" = New-LocalPassword
  }
  foreach ($key in $wanted.Keys) {
    $index = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
      if ($lines[$i] -match "^\s*$([regex]::Escape($key))\s*=") { $index = $i; break }
    }
    if ($index -ge 0) { $lines[$index] = "$key=$($wanted[$key])" } else { $lines.Add("$key=$($wanted[$key])") }
  }
  # LF endings: Compose reads .env raw, so a CR would become part of the password.
  [System.IO.File]::WriteAllText($envPath, (($lines -join "`n") + "`n"))
}

Set-LocalSeed 'Accounts__AdminSeed' 'admin@localhost.test'
Set-LocalSeed 'Accounts__UserSeed' 'user@localhost.test'
Write-Host 'Local Admin: admin@localhost.test, local User: user@localhost.test; passwords are in .env'

if (-not $SkipCertificate) {
  & (Join-Path $PSScriptRoot 'create-dev-cert.ps1')
}

python (Join-Path $PSScriptRoot 'local-status.py')
