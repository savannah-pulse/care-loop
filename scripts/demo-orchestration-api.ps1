#Requires -Version 7
<#
.SYNOPSIS
  Interactive, step-by-step demo of the CareLoop orchestration API (synthetic data only).

.DESCRIPTION
  Walks through the ten-stage case engine: intake, replay safety, conflicts, guardrails, and the
  "no approved routing" path. Each step prints the talking point, the request, and the response, then
  waits for Enter so the presenter controls the pace.

  By default the script builds the API and starts two throwaway instances with temporary SQLite files:
    A (queue configured)  - normal routing, port 5290
    B (no queue)          - shows review work instead of guessing an owner, port 5291
  Use -StartApi $false with -BaseUrl / -NoRoutingUrl to point at servers you already run instead.

.EXAMPLE
  ./scripts/demo-orchestration-api.ps1
  ./scripts/demo-orchestration-api.ps1 -NoPause     # run straight through (smoke test)
#>
[CmdletBinding()]
param(
  [switch] $NoPause,
  [bool] $StartApi = $true,
  [string] $BaseUrl = 'http://localhost:5290',
  [string] $NoRoutingUrl = 'http://localhost:5291'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot 'src/api/CareLoop.Api'
$run = Get-Date -Format 'HHmmss'          # keeps IDs unique so the demo can be re-run against one server
$script:started = @()
$script:tempFiles = @()
$script:step = 0

# ---------- presentation helpers ----------
function Write-Say([string] $Text) { Write-Host "  SAY: $Text" -ForegroundColor Cyan }

function Wait-Presenter {
  if (-not $NoPause) { [void](Read-Host '  [Enter to continue]') }
}

function Start-Step([string] $Title, [string] $Say) {
  $script:step++
  Write-Host ''
  Write-Host ('=' * 78) -ForegroundColor DarkGray
  Write-Host "STEP $($script:step): $Title" -ForegroundColor Green
  if ($Say) { Write-Say $Say }
  Write-Host ('=' * 78) -ForegroundColor DarkGray
  Wait-Presenter
}

function Invoke-Api {
  param([string] $Method, [string] $Path, $Body = $null, [string] $Base = $BaseUrl)

  Write-Host "-> $Method $Path" -ForegroundColor Yellow
  $params = @{ Uri = "$Base$Path"; Method = $Method; SkipHttpErrorCheck = $true }
  if ($Body) {
    $json = $Body | ConvertTo-Json -Depth 6
    Write-Host $json -ForegroundColor DarkGray
    $params.ContentType = 'application/json'
    $params.Body = $json
  }

  $response = Invoke-WebRequest @params
  $text = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { [string] $response.Content }
  $parsed = if ($text) { try { $text | ConvertFrom-Json -Depth 10 } catch { $null } }

  $status = [int] $response.StatusCode
  $color = if ($status -lt 300) { 'Green' } elseif ($status -lt 400) { 'Yellow' } else { 'Red' }
  Write-Host "<- $status $($response.StatusDescription)" -ForegroundColor $color
  if ($parsed) {
    $display = $text | ConvertFrom-Json -Depth 10
    foreach ($noise in 'traceId', 'type') {
      if ($display.PSObject.Properties[$noise]) { $display.PSObject.Properties.Remove($noise) }
    }
    Write-Host ($display | ConvertTo-Json -Depth 8)
  }

  [pscustomobject]@{ Status = $status; Body = $parsed }
}

function Show-Timeline([string] $CaseId, [string] $Base = $BaseUrl) {
  $timeline = Invoke-RestMethod "$Base/api/cases/$CaseId/timeline"
  Write-Host "   Timeline for case $CaseId" -ForegroundColor Yellow
  $timeline | ForEach-Object {
    $stage = if ($_.transition) { $_.transition.to.code } else { '(no stage change)' }
    '   {0}. {1,-14} by {2,-24} {3,-14} {4}' -f $_.sequence, $_.eventType, $_.actorId, $_.disposition, $stage
  } | Write-Host
}

function Show-Exceptions([string] $CaseId, [string] $Base = $BaseUrl) {
  $items = @((Invoke-RestMethod "$Base/api/exceptions?pageSize=100").items | Where-Object caseId -eq $CaseId)
  Write-Host '   Open review work for this case:' -ForegroundColor Yellow
  if ($items.Count -eq 0) { Write-Host '   (none)'; return }
  $items | ForEach-Object { Write-Host ("   - {0}: {1}" -f $_.type, $_.reason) }
}

# ---------- event builders (synthetic only) ----------
function New-ResultEvent([string] $SourceEventId, [string] $ResultRef, [string] $Urgency = 'routine') {
  [ordered]@{
    eventType = 'ResultReceived'; schemaVersion = '1.0'; sourceSystem = 'his-simulator'
    sourceEventId = $SourceEventId; occurredAt = '2026-01-05T08:00:00Z'; synthetic = $true
    patientRef = "SYN-PAT-$run"; resultRef = $ResultRef
    payload = if ($Urgency) { @{ reviewedUrgency = $Urgency } } else { @{} }
  }
}

function New-OwnerEvent([string] $CaseId, [string] $SourceEventId, [string] $Role, [string] $OwnerId) {
  [ordered]@{
    eventType = 'OwnerAssigned'; schemaVersion = '1.0'; sourceSystem = 'careloop-ui'
    sourceEventId = $SourceEventId; occurredAt = '2026-01-05T09:00:00Z'; synthetic = $true; caseId = $CaseId
    actor = @{ kind = 'human'; id = 'staff-1'; role = $Role }
    payload = @{ ownerKind = 'person'; ownerId = $OwnerId }
  }
}

# ---------- server lifecycle ----------
function Start-ApiInstance([string] $Url, [string] $Queue) {
  $db = Join-Path ([IO.Path]::GetTempPath()) "careloop-demo-$([guid]::NewGuid().ToString('N')).db"
  $log = "$db.log"
  $script:tempFiles += $db, $log, "$log.err"

  $env:ASPNETCORE_ENVIRONMENT = 'Demo'
  $env:ASPNETCORE_URLS = $Url
  $env:ConnectionStrings__CareLoop = "Data Source=$db;Pooling=False"
  $env:CareLoop__Database__EnsureCreated = 'true'
  $env:Logging__LogLevel__Default = 'Warning'
  if ($Queue) { $env:CareLoop__OwnerAssignment__DefaultQueueId = $Queue } else { Remove-Item Env:CareLoop__OwnerAssignment__DefaultQueueId -ErrorAction SilentlyContinue }

  $dll = Join-Path $apiProject 'bin/Release/net10.0/CareLoop.Api.dll'
  $process = Start-Process dotnet -ArgumentList $dll -WorkingDirectory $apiProject -PassThru `
    -RedirectStandardOutput $log -RedirectStandardError "$log.err"
  $script:started += $process

  foreach ($attempt in 1..40) {
    try { Invoke-RestMethod "$Url/health" -TimeoutSec 2 | Out-Null; return } catch { Start-Sleep -Milliseconds 500 }
  }
  throw "API at $Url did not start. See $log.err"
}

function Stop-Demo {
  foreach ($process in $script:started) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
  Start-Sleep -Seconds 1
  foreach ($file in $script:tempFiles) {
    foreach ($candidate in $file, "$file-shm", "$file-wal") { Remove-Item $candidate -ErrorAction SilentlyContinue }
  }
}

# ---------- the demo ----------
try {
  if ($StartApi) {
    Write-Host 'Building and starting the API (two throwaway instances)...' -ForegroundColor DarkGray
    dotnet build $apiProject --configuration Release --verbosity quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Start-ApiInstance -Url $BaseUrl -Queue 'synthetic-facility-lead-queue'
    Start-ApiInstance -Url $NoRoutingUrl -Queue $null
  }

  Write-Host ''
  Write-Host 'CareLoop orchestration API demo - synthetic data only' -ForegroundColor Green
  Write-Say 'One actionable screening result becomes one case that moves through ten guarded stages.'
  Write-Say 'The API is the only authority for case state. Everything we show is an appended, audited event.'
  Wait-Presenter

  Start-Step 'The ten-stage map' 'Each stage has one driver. Stages 2 and 3 are the API itself; HIS, the patient channel and clinicians drive the rest.'
  $stages = Invoke-RestMethod "$BaseUrl/api/stages"
  $stages | ForEach-Object { '  {0,2}  {1,-24} entered by {2,-22} driver: {3}' -f $_.number, $_.code, $_.entryEventType, $_.driver } | Write-Host
  Write-Say 'Built so far: stages 1-3. Stages 4-10 plug in as new handlers without editing shared code.'

  Start-Step 'Happy path: one HIS event, three stages' 'The HIS simulator sends a single ResultReceived event. Watch the stage in the response.'
  $first = New-ResultEvent "demo-$run-001" "SYN-RES-$run-A"
  $created = Invoke-Api POST '/api/events' $first
  $caseId = $created.Body.caseId
  Write-Say 'We sent one event, but the API opened the case, created it, and put it in the accountable queue: stage 3.'
  Show-Timeline $caseId
  [void](Invoke-Api GET "/api/cases/$caseId")
  Write-Say 'Every stage change is linked to the event that caused it, and the owner is never empty.'

  Start-Step 'Replay safety: the same event again' 'Networks retry. Sending the identical event must not create a second case or double-apply.'
  [void](Invoke-Api POST '/api/events' $first)
  Write-Say 'Status 200 "duplicate" with the original IDs. The timeline is unchanged:'
  Show-Timeline $caseId

  Start-Step 'Conflict: same source ID, different content' 'If a source reuses an ID with different content, we never silently overwrite. A human has to review it.'
  [void](Invoke-Api POST '/api/events' (New-ResultEvent "demo-$run-001" "SYN-RES-$run-A" 'urgent'))
  Write-Say 'Refused with 409, case untouched, and visible review work was raised:'
  Show-Exceptions $caseId

  Start-Step 'Conflict: a second event for a result that already has a case' 'A correction or duplicate with a new event ID is recorded for audit but never applied automatically.'
  [void](Invoke-Api POST '/api/events' (New-ResultEvent "demo-$run-002" "SYN-RES-$run-A"))
  Show-Timeline $caseId
  Write-Say 'Note the heldForReview entry: it is in the audit trail, but the stage did not change.'

  Start-Step 'Guardrail: no guessing urgency' 'Urgency comes from a reviewing human at the source. If it is missing we raise review work instead of inventing one.'
  $noUrgency = Invoke-Api POST '/api/events' (New-ResultEvent "demo-$run-003" "SYN-RES-$run-B" $null)
  Show-Exceptions $noUrgency.Body.caseId

  Start-Step 'Guardrail: synthetic data only' 'The prototype refuses anything not flagged synthetic.'
  $real = New-ResultEvent "demo-$run-004" "SYN-RES-$run-C"
  $real.synthetic = $false
  [void](Invoke-Api POST '/api/events' $real)

  Start-Step 'Guardrail: who may do what' 'Authorization is enforced by the API, not the UI. A clinician cannot assign owners; a coordinator cannot reassign after assignment yet.'
  [void](Invoke-Api POST '/api/events' (New-OwnerEvent $caseId "demo-$run-own1" 'clinician' 'clinician-9'))
  [void](Invoke-Api POST '/api/events' (New-OwnerEvent $caseId "demo-$run-own2" 'coordinator' 'coordinator-2'))
  Write-Say 'First is 403 (role not permitted). Second is 409: owner transfer needs offer-and-accept, which is a later story.'

  Start-Step 'Guardrail: stages not built yet are refused clearly' 'Events for later stages are not silently dropped.'
  $notify = [ordered]@{
    eventType = 'PatientNotified'; schemaVersion = '1.0'; sourceSystem = 'channel-simulator'
    sourceEventId = "demo-$run-notify"; occurredAt = '2026-01-05T10:00:00Z'; synthetic = $true; caseId = $caseId
  }
  [void](Invoke-Api POST '/api/events' $notify)
  Write-Say '422 unsupported_event_type. Stage 4 is the next piece for the patient-channel workstream.'

  Start-Step 'No approved routing: the case waits, visibly' 'On this second instance no owner queue is configured. We do not invent one.'
  $noRoute = Invoke-Api POST '/api/events' (New-ResultEvent "demo-$run-100" "SYN-RES-$run-D") -Base $NoRoutingUrl
  $waitingId = $noRoute.Body.caseId
  Show-Timeline $waitingId $NoRoutingUrl
  Show-Exceptions $waitingId $NoRoutingUrl
  Write-Say 'The case stops at stage 2 (CASE_CREATED) with OwnerUnassigned raised. Nothing is lost, and nothing advances on a guess.'

  Start-Step 'A coordinator assigns the owner' 'A person with the right role resolves it, and the case moves to stage 3.'
  [void](Invoke-Api POST '/api/events' (New-OwnerEvent $waitingId "demo-$run-assign" 'coordinator' 'coordinator-1') -Base $NoRoutingUrl)
  Show-Timeline $waitingId $NoRoutingUrl
  Show-Exceptions $waitingId $NoRoutingUrl
  Write-Say 'The exception cleared when the owner was assigned. The same history shows who did what and when.'

  Start-Step 'Read models the dashboard will use' 'The UI is read-only. It reads the worklist, timeline and exception queue from these endpoints.'
  $owned = Invoke-RestMethod "$BaseUrl/api/cases?stage=OWNER_ASSIGNED&pageSize=100"
  Write-Host "   Cases at OWNER_ASSIGNED: $($owned.totalCount)"
  $open = Invoke-RestMethod "$BaseUrl/api/exceptions?pageSize=100"
  Write-Host "   Open exceptions: $($open.totalCount)"
  $open.items | ForEach-Object { Write-Host ("   - {0,-24} {1}" -f $_.type, $_.reason) }

  Write-Host ''
  Write-Host 'Recap' -ForegroundColor Green
  Write-Say 'Idempotent intake, sequential guarded stages, conflicts and gaps become visible review work, and the API enforces roles.'
  Write-Say 'Next: stages 4-5 (patient channel), 6-8 (HIS), 9-10 (clinician closure with evidence), and the deadline worker.'
}
finally {
  if ($StartApi) { Stop-Demo }
}
