param(
    [string]$Owner = "ipricetyler-spec",
    [string]$Repo = "open-measurement-evidence-toolkit",
    [string]$DefaultBranch = "main",
    [string]$OutputMarkdown = "artifacts/weekly-oss-health-report.md",
    [string]$OutputJson = "artifacts/weekly-oss-health-report.json",
    [string]$MaintainerMention = "ipricetyler-spec",
    [int]$StaleDaysThreshold = 90,
    [int]$RemoteLinkTimeoutSec = 18,
    [int]$MaxRemoteLinkChecks = 150
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Split-Path -Parent $PSScriptRoot)
$artifactsPath = Join-Path $repoRoot.Path "artifacts"
if (-not (Test-Path $artifactsPath)) {
    New-Item -ItemType Directory -Path $artifactsPath | Out-Null
}

$generatedAtUtc = (Get-Date).ToUniversalTime()
$eventName = if ($env:GITHUB_EVENT_NAME) { $env:GITHUB_EVENT_NAME } else { "manual" }
$isScheduledRun = $eventName -eq "schedule"
$runId = $env:GITHUB_RUN_ID
$runUrl = if ($runId) { "https://github.com/$Owner/$Repo/actions/runs/$runId" } else { $null }
$actor = if ($env:GITHUB_ACTOR) { $env:GITHUB_ACTOR } else { "unknown" }
$week = [System.Globalization.CultureInfo]::InvariantCulture.Calendar.GetWeekOfYear(
    $generatedAtUtc,
    [System.Globalization.CalendarWeekRule]::FirstFourDayWeek,
    [System.DayOfWeek]::Monday
)
$weekKey = "{0}-W{1:00}" -f $generatedAtUtc.ToString("yyyy"), $week
$issueTitle = "OSS maintenance drift detected - $Owner/$Repo - $weekKey"

$apiBase = "https://api.github.com/repos/$Owner/$Repo"
$token = if ($env:GITHUB_TOKEN) { $env:GITHUB_TOKEN } elseif ($env:GH_TOKEN) { $env:GH_TOKEN } else { $null }
$headers = @{
    "User-Agent" = "oss-maintenance-reporter/1.0"
    "Accept" = "application/vnd.github+json"
}
if ($token) {
    $headers.Authorization = "Bearer $token"
}

$taskLog = New-Object System.Collections.Generic.List[object]
$actionableItems = New-Object System.Collections.Generic.List[string]
$informationalItems = New-Object System.Collections.Generic.List[string]

$openIssues = $null
$openPullRequests = $null
$staleIssueCandidates = $null
$stalePullRequestCandidates = $null

$ciRunState = $null
$lintRunState = $null
$workflowRunStatuses = @{}
$coverageState = @{
    has_coverage_artifacts = $false
    artifact_names = @()
    latest_upload_days = $null
}
$securityState = @{
    dependabot_open_alerts = $null
    secret_scanning_open_alerts = $null
    vulnerability_alerts_enabled = $false
    vulnerability_alerts_status = "unknown"
}
$dependabotConfig = @{
    exists = $false
    open_pull_requests_limit_zero = $false
    ignores_semver_major = $false
}
$branchProtectionState = @{
    has_protection = $false
    required_approvals = $null
    dismiss_stale_reviews = $null
    strict_status_checks = $null
}
$licenseState = @{
    api_license_present = $false
    local_license_present = $false
    local_license_spdx_like = $false
    spdx_id = $null
    third_party_notices_present = $false
}
$requiredFilesState = @{}
$documentationChecks = @{}
$deadLinkFindings = New-Object System.Collections.Generic.List[object]
$maintenanceIssue = @{ status = "not_triggered" }

function Add-TaskLog {
    param([string]$Step, [string]$Status, [string]$Message, [object]$Metadata = $null)
    $entry = @{
        step = $Step
        status = $Status
        message = $Message
        timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
    }
    if ($Metadata -ne $null) {
        $entry.metadata = $Metadata
    }
    $taskLog.Add($entry) | Out-Null
    Write-Output "[$($Status.ToUpper())] $Step - $Message"
}

function Add-Actionable {
    param([string]$Item)
    if (-not $actionableItems.Contains($Item)) {
        $actionableItems.Add($Item) | Out-Null
    }
}

function Add-Info {
    param([string]$Item)
    if (-not $informationalItems.Contains($Item)) {
        $informationalItems.Add($Item) | Out-Null
    }
}

function Invoke-GitHubApi {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [ValidateSet("Get", "Post", "Patch", "Delete")][string]$Method = "Get",
        [object]$Body = $null
    )

    $uri = if ($Path -like "http*://*") { $Path } else { "$apiBase/$Path" }

    try {
        $request = @{
            Uri             = $uri
            Method          = $Method
            Headers         = $headers
            ErrorAction     = "Stop"
            UseBasicParsing = $true
            TimeoutSec      = 45
        }
        if ($Body -ne $null) {
            $request.Body = ($Body | ConvertTo-Json -Depth 12)
            $request.ContentType = "application/json"
        }

        $response = Invoke-WebRequest @request
        $payload = $null
        if ($response.Content) {
            try {
                $payload = $response.Content | ConvertFrom-Json -ErrorAction Stop
            } catch {
                $payload = $response.Content
            }
        }

        return @{
            Success = $true
            StatusCode = [int]$response.StatusCode
            Data = $payload
            Headers = $response.Headers
        }
    } catch {
        $statusCode = $null
        $message = $_.Exception.Message
        if ($_.Exception.Response) {
            try {
                $statusCode = [int]$_.Exception.Response.StatusCode.value__
            } catch {
                $statusCode = $null
            }
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                if ($stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $message = $reader.ReadToEnd()
                }
            } catch {
                # no-op, keep base message
            }
        }
        return @{
            Success = $false
            StatusCode = $statusCode
            Error = $message
            Data = $null
        }
    }
}

function Get-SearchCount {
    param([string]$Query)
    $escaped = [System.Uri]::EscapeDataString($Query)
    $result = Invoke-GitHubApi "search/issues?q=$escaped&per_page=1"
    if (-not $result.Success) {
        return $null
    }
    if (-not $result.Data -or -not ($result.Data.PSObject.Properties.Name -contains "total_count")) {
        return $null
    }
    return [int]$result.Data.total_count
}

function Get-EndpointCount {
    param([string]$Path)
    $result = Invoke-GitHubApi $Path
    if (-not $result.Success) {
        return $null
    }
    if ($result.Data -and ($result.Data.PSObject.Properties.Name -contains "total_count")) {
        return [int]$result.Data.total_count
    }
    if ($result.Data -is [array]) {
        return [int]$result.Data.Count
    }
    return $null
}

function Get-LatestWorkflowRun {
    param([string]$WorkflowFile, [string]$Branch)
    $encodedWorkflow = [System.Uri]::EscapeDataString($WorkflowFile)
    $query = "actions/workflows/$encodedWorkflow/runs?per_page=1&branch=$Branch"
    $result = Invoke-GitHubApi $query
    if (-not $result.Success) {
        return @{
            Success = $false
            Error = $result.Error
            StatusCode = $result.StatusCode
            HasRun = $false
        }
    }

    if ($null -eq $result.Data.workflow_runs -or $result.Data.workflow_runs.Count -eq 0) {
        return @{ Success = $true; HasRun = $false }
    }

    $run = $result.Data.workflow_runs[0]
    return @{
        Success = $true
        HasRun = $true
        Status = $run.status
        Conclusion = $run.conclusion
        Url = $run.html_url
        UpdatedAt = $run.updated_at
        CreatedAt = $run.created_at
        Event = $run.event
    }
}

function Extract-MarkdownLinks {
    param([string]$Path)
    $text = Get-Content -Raw -Path $Path
    $links = New-Object System.Collections.Generic.List[string]

    $inlineMatches = [regex]'\[[^\]]*\]\((?<link>[^)\s]+)(?:\s+"[^"]*")?\)'
    foreach ($m in $inlineMatches.Matches($text)) {
        $links.Add($m.Groups["link"].Value.Trim())
    }

    $referenceMatches = [regex]'(?m)^\s*\[[^\]]+\]:\s*(?<link>\S+)'
    foreach ($m in $referenceMatches.Matches($text)) {
        $links.Add($m.Groups["link"].Value.Trim())
    }

    $angleMatches = [regex]'<(?<link>https?://[^>\s]+)>'
    foreach ($m in $angleMatches.Matches($text)) {
        $links.Add($m.Groups["link"].Value.Trim())
    }

    $unique = New-Object System.Collections.Generic.List[string]
    $seen = @{}
    foreach ($link in $links) {
        if ([string]::IsNullOrWhiteSpace($link)) { continue }
        if (-not $seen.ContainsKey($link)) {
            $seen[$link] = $true
            $unique.Add($link)
        }
    }
    return $unique
}

function Resolve-MarkdownLinkPath {
    param([string]$Path, [string]$SourceFile)
    $candidate = $Path.Split('#')[0].Split('?')[0]
    if ([string]::IsNullOrWhiteSpace($candidate)) { return $null }

    if ($candidate -match '^(https?|ftp):\/\/') { return $null }
    if ($candidate -match '^mailto:|^tel:|^javascript:|^data:|^#') { return $null }

    $resolved = if ($candidate -like "/*") {
        Join-Path $repoRoot.Path $candidate.TrimStart("/")
    } else {
        Join-Path (Split-Path -Path $SourceFile -Parent) $candidate
    }

    try {
        return [System.IO.Path]::GetFullPath($resolved)
    } catch {
        return $null
    }
}

function Check-RemoteLink {
    param([string]$Url, [int]$TimeoutSec)
    try {
        $head = Invoke-WebRequest -Uri $Url -Method Head -MaximumRedirection 5 -TimeoutSec $TimeoutSec -UseBasicParsing -Headers @{ "User-Agent" = "oss-maintenance-reporter/1.0" }
        if ($head.StatusCode -ge 200 -and $head.StatusCode -lt 400) {
            return @{ IsHealthy = $true; Inconclusive = $false; Status = [int]$head.StatusCode }
        }
        return @{ IsHealthy = $false; Inconclusive = $false; Status = [int]$head.StatusCode }
    } catch {
        if ($_.Exception.Response) {
            $statusCode = $null
            try {
                $statusCode = [int]$_.Exception.Response.StatusCode.value__
            } catch { }
            if ($statusCode -eq 405 -or $statusCode -eq 301 -or $statusCode -eq 302 -or $statusCode -eq 307) {
                try {
                    $get = Invoke-WebRequest -Uri $Url -Method Get -MaximumRedirection 5 -TimeoutSec $TimeoutSec -UseBasicParsing -Headers @{ "User-Agent" = "oss-maintenance-reporter/1.0" }
                    if ($get.StatusCode -ge 200 -and $get.StatusCode -lt 400) {
                        return @{ IsHealthy = $true; Inconclusive = $false; Status = [int]$get.StatusCode }
                    }
                    return @{ IsHealthy = $false; Inconclusive = $false; Status = [int]$get.StatusCode }
                } catch {
                    # fallthrough to response based evaluation below
                }
            }

            if ($statusCode -in 403, 429, 451, 503) {
                return @{ IsHealthy = $false; Inconclusive = $true; Status = $statusCode }
            }
            return @{ IsHealthy = $false; Inconclusive = $false; Status = $statusCode }
        }
        return @{ IsHealthy = $false; Inconclusive = $true; Status = $null; Error = "Connection error" }
    }
}

function Check-DocumentationLinks {
    $files = Get-ChildItem -Path $repoRoot.Path -Recurse -File -Include *.md, *.markdown, *.mdx -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FullName -notmatch '\\\.git(\\|$)' -and
            $_.FullName -notmatch '\\artifacts(\\|$)' -and
            $_.FullName -notmatch '\\node_modules(\\|$)' -and
            $_.FullName -notmatch '\\bin(\\|$)' -and
            $_.FullName -notmatch '\\obj(\\|$)' -and
            $_.FullName -notmatch '\\dist(\\|$)'
        }

    $checkedLinks = 0
    $checkedFiles = if ($files) { $files.Count } else { 0 }
    $remoteChecked = 0
    $actionable = 0
    $warnings = 0
    $findings = New-Object System.Collections.Generic.List[object]
    $seen = @{}

    foreach ($file in $files) {
        try {
            $links = Extract-MarkdownLinks -Path $file.FullName
        } catch {
            Add-Info "Could not parse links from file $($file.FullName): $($_.Exception.Message)"
            continue
        }

        foreach ($link in $links) {
            $key = "$($file.FullName)::${link}"
            if ($seen.ContainsKey($key)) { continue }
            $seen[$key] = $true

            $trimmed = $link.Trim()
            if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
            if ($trimmed -match '^https?:\/\/') {
                if ($remoteChecked -ge $MaxRemoteLinkChecks) { continue }
                $remoteChecked += 1
                $checkedLinks += 1
                $result = Check-RemoteLink -Url $trimmed -TimeoutSec $RemoteLinkTimeoutSec
                if (-not $result.IsHealthy) {
                    if ($result.Inconclusive) {
                        $warnings++
                        $findings.Add([ordered]@{
                            file = $file.FullName
                            url = $trimmed
                            severity = "warning"
                            reason = "Could not confidently validate remote link"
                            status = if ($null -ne $result.Status) { $result.Status } else { "inconclusive" }
                        }) | Out-Null
                    } else {
                        $actionable++
                        $findings.Add([ordered]@{
                            file = $file.FullName
                            url = $trimmed
                            severity = "actionable"
                            reason = "Remote link returned non-2xx/3xx status"
                            status = if ($null -ne $result.Status) { $result.Status } else { "unknown" }
                        }) | Out-Null
                    }
                }
                continue
            }

            if ($trimmed -match '^mailto:|^tel:|^javascript:|^data:|^#|^\\s*$') { continue }

            $checkedLinks += 1
            $resolved = Resolve-MarkdownLinkPath -Path $trimmed -SourceFile $file.FullName
            if ([string]::IsNullOrWhiteSpace($resolved)) {
                continue
            }
            if (-not $resolved.StartsWith($repoRoot.Path, [System.StringComparison]::OrdinalIgnoreCase)) {
                # Relative paths pointing outside repo scope are not treated as hard failures.
                Add-Info "Ignoring path-traversal candidate link: $trimmed in $($file.FullName)"
                continue
            }
            if (-not (Test-Path -LiteralPath $resolved)) {
                $actionable++
                $findings.Add([ordered]@{
                    file = $file.FullName
                    url = $trimmed
                    severity = "actionable"
                    reason = "Missing local file target"
                    resolved_path = $resolved
                }) | Out-Null
            }
        }
    }

    return @{
        files_scanned = $checkedFiles
        links_scanned = $checkedLinks
        remote_links_checked = $remoteChecked
        actionable_count = $actionable
        warning_count = $warnings
        findings = $findings
    }
}

function Create-OrUpdate-Issue {
    param([string]$Title, [string]$Body)
    if (-not $token) {
        Add-TaskLog "maintenance-issue" "info" "GITHUB_TOKEN missing; cannot create or update maintenance issue."
        return @{ status = "skipped_no_token" }
    }

    $openIssues = Invoke-GitHubApi "issues?state=open&per_page=100"
    if (-not $openIssues.Success) {
        Add-TaskLog "maintenance-issue" "warning" "Issue query failed: $($openIssues.Error)"
        return @{ status = "query_failed"; error = $openIssues.Error }
    }

    $existing = $null
    foreach ($issue in $openIssues.Data) {
        if ($issue.title -eq $Title) {
            $existing = $issue
            break
        }
    }

    $mention = if ([string]::IsNullOrWhiteSpace($MaintainerMention)) { $Owner } else { $MaintainerMention }
    $mentionLine = "Maintainers: @$mention"
    $payload = "$mentionLine`n`n" + $Body

    if ($null -ne $existing) {
    $commentBody = @"
Maintainers: @$mention

Week: $weekKey
Generated: $($generatedAtUtc.ToString("o"))
Run: $runUrl

`n### Actionable findings for this run
$(if ($actionableItems.Count -gt 0) { "- " + ($actionableItems -join "`n- ") } else { "- None" })
"@
        $comment = Invoke-GitHubApi "repos/$Owner/$Repo/issues/$($existing.number)/comments" -Method Post -Body @{ body = $commentBody }
        if ($comment.Success) {
            Add-TaskLog "maintenance-issue" "passed" "Updated existing weekly issue #$($existing.number)."
            return @{ status = "updated"; issue_number = $existing.number; issue_url = $existing.html_url }
        }
        Add-TaskLog "maintenance-issue" "warning" "Existing issue found but update failed."
        return @{ status = "update_failed"; error = $comment.Error }
    }

    $issueBody = "$payload`n`n$body"
    $created = Invoke-GitHubApi "repos/$Owner/$Repo/issues" -Method Post -Body @{ title = $Title; body = $issueBody }
    if ($created.Success) {
        Add-TaskLog "maintenance-issue" "passed" "Created maintenance issue #$($created.Data.number)."
        return @{ status = "created"; issue_number = $created.Data.number; issue_url = $created.Data.html_url }
    }
    Add-TaskLog "maintenance-issue" "warning" "Could not create maintenance issue: $($created.Error)"
    return @{ status = "create_failed"; error = $created.Error }
}

function Ensure-LogSummary {
    param([string]$Message)
    if (-not (Test-Path $artifactsPath)) {
        New-Item -ItemType Directory -Path $artifactsPath | Out-Null
    }
    Write-Output $Message
}

function Get-WorkflowSummaryText {
    param($WorkflowState)
    if ($null -eq $WorkflowState) {
        return "unavailable"
    }
    if ($WorkflowState.Success -eq $false) {
        return "query_failed"
    }
    if (-not $WorkflowState.HasRun) {
        return "not_run"
    }
    if ($null -ne $WorkflowState.Status -and $null -ne $WorkflowState.Conclusion) {
        return "$($WorkflowState.Status)-$($WorkflowState.Conclusion)"
    }
    return "unknown"
}

function Format-OptionalValue {
    param($Value)
    if ($null -eq $Value) { return "N/A" }
    return $Value
}

Ensure-LogSummary -Message "Repository: $Owner/$Repo"
Ensure-LogSummary -Message "Run context: event=$eventName, actor=$actor, week=$weekKey"

# 1) Open work and stale candidates
try {
    $staleCutoffDate = $generatedAtUtc.AddDays(-$StaleDaysThreshold).ToString("yyyy-MM-dd")
    $openIssues = Get-SearchCount "repo:$Owner/$Repo is:issue is:open"
    $openPullRequests = Get-SearchCount "repo:$Owner/$Repo is:pr is:open"
    $staleIssueCandidates = Get-SearchCount "repo:$Owner/$Repo is:issue is:open updated:<$staleCutoffDate"
    $stalePullRequestCandidates = Get-SearchCount "repo:$Owner/$Repo is:pr is:open updated:<$staleCutoffDate"

    if ($openIssues -ne $null) {
        Add-TaskLog "open-work-scan" "passed" "Open issue count: $openIssues"
    } else {
        Add-TaskLog "open-work-scan" "warning" "Could not load open issue count."
        Add-Info "Open issue count could not be verified."
    }

    if ($openPullRequests -ne $null) {
        Add-TaskLog "open-work-scan" "passed" "Open PR count: $openPullRequests"
    } else {
        Add-TaskLog "open-work-scan" "warning" "Could not load open PR count."
        Add-Info "Open PR count could not be verified."
    }

    if (($openIssues -ne $null) -and ($openIssues -gt 40)) {
        Add-Actionable "Open issues are above 40 ($openIssues)."
    }
    if (($openPullRequests -ne $null) -and ($openPullRequests -gt 12)) {
        Add-Actionable "Open PRs are above 12 ($openPullRequests)."
    }

    if (($staleIssueCandidates -ne $null -and $staleIssueCandidates -gt 0) -or ($stalePullRequestCandidates -ne $null -and $stalePullRequestCandidates -gt 0)) {
        Add-Info "Stale candidate count (older than $StaleDaysThreshold days): $staleIssueCandidates issues / $stalePullRequestCandidates PRs."
    }
} catch {
    Add-TaskLog "open-work-scan" "failed" "Error scanning open work: $($_.Exception.Message)"
    Add-Info "Work queue scan encountered an API error."
}

# 2) CI and lint/format workflow checks
try {
    $ciRunState = Get-LatestWorkflowRun -WorkflowFile "ci.yml" -Branch $DefaultBranch
    $lintRunState = Get-LatestWorkflowRun -WorkflowFile "pr-lint-format.yml" -Branch $DefaultBranch
    $workflowRunStatuses["ci.yml"] = $ciRunState
    $workflowRunStatuses["pr-lint-format.yml"] = $lintRunState

    if ($ciRunState.Success -and $ciRunState.HasRun) {
        Add-TaskLog "ci-workflow-check" "passed" "ci.yml latest status: $($ciRunState.Status) / $($ciRunState.Conclusion)"
        if ($ciRunState.Status -eq "completed" -and $ciRunState.Conclusion -ne "success") {
            Add-Actionable "CI workflow failed on latest run: $($ciRunState.Conclusion)."
        }
    } elseif (-not $ciRunState.Success) {
        Add-TaskLog "ci-workflow-check" "failed" "Unable to read ci.yml workflow runs."
        Add-Info "CI workflow status unavailable via API."
    } else {
        Add-TaskLog "ci-workflow-check" "warning" "No ci workflow runs found for default branch."
        Add-Info "No ci.yml runs found for branch $DefaultBranch."
    }

    if ($lintRunState.Success -and $lintRunState.HasRun) {
        Add-TaskLog "lint-workflow-check" "passed" "pr-lint-format latest status: $($lintRunState.Status) / $($lintRunState.Conclusion)"
        if ($lintRunState.Status -eq "completed" -and $lintRunState.Conclusion -ne "success") {
            Add-Actionable "PR lint/format workflow failed on latest run: $($lintRunState.Conclusion)."
        }
    } elseif (-not $lintRunState.Success) {
        Add-TaskLog "lint-workflow-check" "failed" "Unable to read pr-lint-format workflow runs."
        Add-Info "pr-lint-format status unavailable via API."
    } else {
        Add-TaskLog "lint-workflow-check" "warning" "No pr-lint-format workflow runs found."
        Add-Info "No pr-lint-format runs found for branch $DefaultBranch."
    }
} catch {
    Add-TaskLog "ci-workflow-check" "failed" "Workflow check failed: $($_.Exception.Message)"
    Add-Info "Workflow status checks had an API error."
}

# 3) Security posture: secret scanning + dependabot alerts + vulnerability alerts
try {
    $securityState.dependabot_open_alerts = Get-EndpointCount "dependabot/alerts?state=open&per_page=100"
    $securityState.secret_scanning_open_alerts = Get-EndpointCount "secret-scanning/alerts?state=open&per_page=100"
    $vulnEnabledResponse = Invoke-GitHubApi "vulnerability-alerts"
    if ($vulnEnabledResponse.Success -and $vulnEnabledResponse.StatusCode -eq 204) {
        $securityState.vulnerability_alerts_enabled = $true
        $securityState.vulnerability_alerts_status = "enabled"
    } elseif ($vulnEnabledResponse.StatusCode -eq 404) {
        $securityState.vulnerability_alerts_enabled = $false
        $securityState.vulnerability_alerts_status = "not_enabled"
        Add-Actionable "Dependabot vulnerability alerts are not enabled."
    } else {
        $securityState.vulnerability_alerts_enabled = $false
        $securityState.vulnerability_alerts_status = "unknown"
        Add-Info "Could not verify vulnerability alert status."
    }

    if ($securityState.dependabot_open_alerts -ne $null -and $securityState.dependabot_open_alerts -gt 0) {
        Add-TaskLog "security-posture" "failed" "Dependabot open alerts: $($securityState.dependabot_open_alerts)"
        Add-Actionable "Open Dependabot security alerts found: $($securityState.dependabot_open_alerts)."
    } elseif ($securityState.dependabot_open_alerts -eq 0) {
        Add-TaskLog "security-posture" "passed" "No open Dependabot alerts."
    } else {
        Add-TaskLog "security-posture" "warning" "Dependabot alert count unavailable."
        Add-Info "Could not load Dependabot alerts."
    }

    if ($securityState.secret_scanning_open_alerts -ne $null -and $securityState.secret_scanning_open_alerts -gt 0) {
        Add-TaskLog "security-posture" "failed" "Secret scanning open alerts: $($securityState.secret_scanning_open_alerts)"
        Add-Actionable "Open GitHub secret-scanning alerts found: $($securityState.secret_scanning_open_alerts)."
    } elseif ($securityState.secret_scanning_open_alerts -eq 0) {
        Add-TaskLog "security-posture" "passed" "No open secret-scanning alerts."
    } else {
        Add-TaskLog "security-posture" "warning" "Secret scan alert count unavailable."
        Add-Info "Secret scan alerts could not be queried with current permissions."
    }
} catch {
    Add-TaskLog "security-posture" "failed" "Security checks failed: $($_.Exception.Message)"
    Add-Info "Security scan collection hit API exception."
}

# 4) Branch protection checks
try {
    $branchResponse = Invoke-GitHubApi "branches/$DefaultBranch/protection"
    if (-not $branchResponse.Success) {
        if ($branchResponse.StatusCode -eq 404) {
            Add-TaskLog "branch-protection" "failed" "No branch protection configured for $DefaultBranch."
            Add-Actionable "Branch protection missing on '$DefaultBranch'."
            $branchProtectionState.has_protection = $false
        } else {
            Add-TaskLog "branch-protection" "warning" "Branch protection could not be verified."
            Add-Info "Branch protection API response: $($branchResponse.StatusCode)."
            $branchProtectionState.has_protection = $false
        }
    } else {
        $branchProtectionState.has_protection = $true
        $requiredReviews = $branchResponse.Data.required_pull_request_reviews
        if ($null -ne $requiredReviews) {
            $branchProtectionState.required_approvals = $requiredReviews.required_approving_review_count
            $branchProtectionState.dismiss_stale_reviews = $requiredReviews.dismiss_stale_reviews
            if ($requiredReviews.required_approving_review_count -lt 1) {
                Add-Actionable "Branch protection does not require at least 1 approving review."
            }
            if (-not $requiredReviews.dismiss_stale_reviews) {
                Add-Actionable "Branch protection does not dismiss stale reviews."
            }
        } else {
            Add-Actionable "Branch protection review requirements are not configured."
        }

        $requiredChecks = $branchResponse.Data.required_status_checks
        if ($null -ne $requiredChecks) {
            $branchProtectionState.strict_status_checks = $requiredChecks.strict
            if (-not $requiredChecks.strict) {
                Add-Info "Branch protection status checks are enabled but strict mode is disabled."
            }
        } else {
            Add-Actionable "Branch protection does not enforce required status checks."
        }
        Add-TaskLog "branch-protection" "passed" "Branch protection verified."
    }
} catch {
    Add-TaskLog "branch-protection" "failed" "Branch protection check failed: $($_.Exception.Message)"
    Add-Info "Could not validate branch protection."
}

# 5) License and repository documentation checks
try {
    $requiredDocs = @("README.md", "LICENSE", "SECURITY.md")
    $recommendedDocs = @("CONTRIBUTING.md", "CODE_OF_CONDUCT.md", "CHANGELOG.md", "THIRD_PARTY_NOTICES.md", "docs/maintenance.md", "ROADMAP.md")

    foreach ($file in $requiredDocs) {
        $exists = Test-Path (Join-Path $repoRoot.Path $file)
        $requiredFilesState[$file] = $exists
        if (-not $exists) {
            Add-Actionable "Required repository file is missing: $file"
        }
    }
    foreach ($file in $recommendedDocs) {
        $exists = Test-Path (Join-Path $repoRoot.Path $file)
        $requiredFilesState[$file] = $exists
        if (-not $exists) {
            Add-Info "Recommended repository file is missing: $file."
        }
    }

    $licenseState.local_license_present = Test-Path (Join-Path $repoRoot.Path "LICENSE")
    $licenseText = if ($licenseState.local_license_present) { Get-Content -Raw (Join-Path $repoRoot.Path "LICENSE") } else { "" }
    $licenseState.local_license_spdx_like = $licenseText -match "Apache License|MIT License|BSD|GNU General Public|MPL|EPL|ISC|Creative Commons|GNU Lesser"

    $licenseResponse = Invoke-GitHubApi "license"
    if ($licenseResponse.Success -and $licenseResponse.Data.license) {
        $licenseState.api_license_present = $true
        $licenseState.spdx_id = $licenseResponse.Data.license.spdx_id
        if ([string]::IsNullOrWhiteSpace($licenseState.spdx_id) -or $licenseState.spdx_id -eq "NOASSERTION") {
            Add-Actionable "Repository license could not be confidently identified via GitHub API."
        } else {
            Add-TaskLog "license-docs" "passed" "License detected: $($licenseState.spdx_id)"
        }
    } elseif ($licenseResponse.Success) {
        Add-TaskLog "license-docs" "warning" "License endpoint returned no license metadata."
        Add-Info "GitHub license API did not return metadata."
    } else {
        Add-TaskLog "license-docs" "warning" "Could not verify license via GitHub API."
        Add-Info "License API unavailable in this run."
    }

    $licenseState.third_party_notices_present = Test-Path (Join-Path $repoRoot.Path "THIRD_PARTY_NOTICES.md")

    if (-not $licenseState.local_license_present) {
        Add-TaskLog "license-docs" "failed" "LICENSE file missing."
    } elseif (-not $licenseState.local_license_spdx_like) {
        Add-Info "LICENSE file exists but does not match common SPDX patterns."
    } else {
        Add-TaskLog "license-docs" "passed" "LICENSE file exists."
    }
} catch {
    Add-TaskLog "license-docs" "failed" "License/documentation checks failed: $($_.Exception.Message)"
    Add-Info "License/doc checks had parsing issues."
}

# 6) Dependabot policy checks
try {
    $dependabotFile = Join-Path $repoRoot.Path ".github/dependabot.yml"
    if (Test-Path $dependabotFile) {
        $dependabotConfig.exists = $true
        $text = Get-Content -Raw -Path $dependabotFile
        if ($text -match "open-pull-requests-limit:\s*0") { $dependabotConfig.open_pull_requests_limit_zero = $true } else { Add-Actionable "Dependabot config does not set open-pull-requests-limit: 0." }
        if ($text -match "version-update:semver-major") { $dependabotConfig.ignores_semver_major = $true } else { Add-Actionable "Dependabot config does not explicitly ignore semver-major updates." }
        Add-TaskLog "dependabot-config" "passed" "Dependabot config exists."
    } else {
        Add-TaskLog "dependabot-config" "failed" "Dependabot config missing."
        Add-Actionable "Dependabot config missing at .github/dependabot.yml."
    }
} catch {
    Add-TaskLog "dependabot-config" "failed" "Dependabot policy check failed: $($_.Exception.Message)"
    Add-Info "Could not validate dependabot configuration."
}

# 7) Coverage artifact presence
try {
    $artifactResponse = Invoke-GitHubApi "actions/artifacts?per_page=100"
    if ($artifactResponse.Success) {
        foreach ($artifact in $artifactResponse.Data.artifacts) {
            if ($artifact.name -match "(?i)(coverage|codecov|cobertura|lcov|opencover)") {
                $coverageState.has_coverage_artifacts = $true
                $coverageState.artifact_names += $artifact.name
                if ($null -eq $coverageState.latest_upload_days) {
                    try {
                        $updated = [datetime]$artifact.updated_at
                        $coverageState.latest_upload_days = [Math]::Round((New-TimeSpan -Start $updated -End $generatedAtUtc).TotalDays, 1)
                    } catch { }
                }
            }
        }
        Add-TaskLog "coverage-check" "passed" "Coverage artifact scan complete."
        if (-not $coverageState.has_coverage_artifacts) {
            Add-Info "No coverage artifact names detected in the last artifact page."
        }
    } else {
        Add-TaskLog "coverage-check" "warning" "Coverage artifacts could not be enumerated."
        Add-Info "Coverage artifact enumeration failed."
    }
} catch {
    Add-TaskLog "coverage-check" "failed" "Coverage check failed: $($_.Exception.Message)"
    Add-Info "Coverage check hit an API exception."
}

# 8) Dead link validation (local + remote)
$deadLinkSummary = @{ files_scanned = 0; links_scanned = 0; remote_links_checked = 0; actionable_count = 0; warning_count = 0; findings = @() }
try {
    $deadLinkSummary = Check-DocumentationLinks
    $documentationChecks = $deadLinkSummary
    if ($deadLinkSummary.actionable_count -gt 0) {
        Add-Actionable "Documentation dead links found: $($deadLinkSummary.actionable_count) (first 150 checked: $($deadLinkSummary.remote_links_checked) remote, $($deadLinkSummary.links_scanned) total)."
    }
    if ($deadLinkSummary.warning_count -gt 0) {
        Add-Info "Documentation link check produced $($deadLinkSummary.warning_count) warning links."
    }
    Add-TaskLog "documentation-links" "passed" "Checked markdown links: files=$($deadLinkSummary.files_scanned), links=$($deadLinkSummary.links_scanned), remote=$($deadLinkSummary.remote_links_checked)."
} catch {
    Add-TaskLog "documentation-links" "failed" "Dead link validation failed: $($_.Exception.Message)"
    Add-Info "Dead link checks could not be completed."
}

$documentationChecks = [ordered]@{
    files_scanned = $deadLinkSummary.files_scanned
    links_scanned = $deadLinkSummary.links_scanned
    remote_links_checked = $deadLinkSummary.remote_links_checked
    actionable_count = $deadLinkSummary.actionable_count
    warning_count = $deadLinkSummary.warning_count
    findings = if ($deadLinkSummary.findings) {
        $deadLinkSummary.findings | Select-Object -First 40
    } else {
        @()
    }
}

if ($token) {
    $actionableItemsText = "- None"
    if ($actionableItems.Count -gt 0) {
        $actionableItemsText = "- " + ($actionableItems -join "`n- ")
    }

    $issueBody = "## Weekly OSS maintenance drift for $Owner/$Repo`r`n" +
        "Generated UTC: $($generatedAtUtc.ToString("o"))`r`n" +
        "Week key: $weekKey`r`n" +
        "Run: $runUrl`r`n" +
        "Event: $eventName`r`n" +
        "Actor: $actor`r`n" +
        "`r`n### Actionable findings`r`n$actionableItemsText`r`n" +
        "`r`n### Workload snapshot`r`n" +
        "- Open issues: $openIssues`r`n" +
        "- Open PRs: $openPullRequests`r`n" +
        "- Stale candidates (>$StaleDaysThreshold days): $staleIssueCandidates issues / $stalePullRequestCandidates PRs`r`n" +
        "`r`n### Coverage and link scan`r`n" +
        "- Coverage artifacts: $($coverageState.has_coverage_artifacts)`r`n" +
        "- Documentation files scanned: $($documentationChecks.files_scanned)`r`n" +
        "- Remote links checked: $($documentationChecks.remote_links_checked)`r`n" +
        "- Actionable dead links: $($documentationChecks.actionable_count)`r`n" +
        "`r`n### Task log`r`n``````json`r`n" +
        "$($taskLog | ConvertTo-Json -Depth 8)`r`n``````"

    $maintenanceIssueResult = Create-OrUpdate-Issue -Title $issueTitle -Body $issueBody
    if ($maintenanceIssueResult -is [array]) {
        $maintenanceIssue = $maintenanceIssueResult | Where-Object { $_ -is [hashtable] } | Select-Object -First 1
    } else {
        $maintenanceIssue = $maintenanceIssueResult
    }
}

$report = [ordered]@{
    generated_utc = $generatedAtUtc.ToString("o")
    repository = "$Owner/$Repo"
    default_branch = $DefaultBranch
    week_key = $weekKey
    run = @{
        event = $eventName
        actor = $actor
        url = $runUrl
    }
    workload = @{
        open_issues = $openIssues
        open_pull_requests = $openPullRequests
        stale_issue_candidates = $staleIssueCandidates
        stale_pull_request_candidates = $stalePullRequestCandidates
        stale_days_threshold = $StaleDaysThreshold
    }
    workflows = @{
        ci = $ciRunState
        pr_lint_format = $lintRunState
    }
    security = $securityState
    dependabot = $dependabotConfig
    branch_protection = $branchProtectionState
    license_and_docs = @{
        required_files = $requiredFilesState
        third_party_notices_present = $licenseState.third_party_notices_present
        local_license_spdx_like = $licenseState.local_license_spdx_like
        api_spdx_id = $licenseState.spdx_id
    }
    coverage = $coverageState
    documentation_links = $documentationChecks
    maintenance_issue = $maintenanceIssue
    actionable_items = $actionableItems
    informational_items = $informationalItems
    task_log = $taskLog
}

$markdownLines = New-Object System.Collections.Generic.List[string]
$ciWorkflowSummary = Get-WorkflowSummaryText $ciRunState
$lintWorkflowSummary = Get-WorkflowSummaryText $lintRunState
$markdownLines.Add("# Weekly OSS Health Report")
$markdownLines.Add("")
$markdownLines.Add("Generated UTC: $($generatedAtUtc.ToString("o"))")
$markdownLines.Add("Repository: https://github.com/$Owner/$Repo")
$markdownLines.Add("Default branch: $DefaultBranch")
$markdownLines.Add("Week key: $weekKey")
$markdownLines.Add("Run context: $eventName ($actor)")
$markdownLines.Add("Run URL: $(if ($runUrl) { $runUrl } else { "N/A" })")
$markdownLines.Add("")
$markdownLines.Add("## Repository work queue")
$markdownLines.Add("- Open issues: $(Format-OptionalValue $openIssues)")
$markdownLines.Add("- Open PRs: $(Format-OptionalValue $openPullRequests)")
$markdownLines.Add("- Stale candidates ($StaleDaysThreshold+ days): $(Format-OptionalValue $staleIssueCandidates) issue(s), $(Format-OptionalValue $stalePullRequestCandidates) PR(s)")
$markdownLines.Add("")
$markdownLines.Add("## Workflow health")
$markdownLines.Add("- ci.yml: $ciWorkflowSummary")
$markdownLines.Add("- pr-lint-format.yml: $lintWorkflowSummary")
$markdownLines.Add("")
$markdownLines.Add("## Security posture")
$markdownLines.Add("- Open Dependabot alerts: $(Format-OptionalValue $securityState.dependabot_open_alerts)")
$markdownLines.Add("- Open secret-scanning alerts: $(Format-OptionalValue $securityState.secret_scanning_open_alerts)")
$markdownLines.Add("- Dependabot vulnerability alerts enabled: $($securityState.vulnerability_alerts_enabled)")
$markdownLines.Add("- Vulnerability alerts API status: $($securityState.vulnerability_alerts_status)")
$markdownLines.Add("")
$markdownLines.Add("## Branch protection")
$markdownLines.Add("- Protected: $($branchProtectionState.has_protection)")
$markdownLines.Add("- Required approvals: $($branchProtectionState.required_approvals)")
$markdownLines.Add("- Dismiss stale reviews: $($branchProtectionState.dismiss_stale_reviews)")
$markdownLines.Add("- Strict status checks: $($branchProtectionState.strict_status_checks)")
$markdownLines.Add("")
$markdownLines.Add("## Dependency policy")
$markdownLines.Add("- Dependabot config exists: $($dependabotConfig.exists)")
$markdownLines.Add("- Dependabot open PR limit 0: $($dependabotConfig.open_pull_requests_limit_zero)")
$markdownLines.Add("- Semver-major ignored: $($dependabotConfig.ignores_semver_major)")
$markdownLines.Add("")
$markdownLines.Add("## Coverage and artifact state")
$markdownLines.Add("- Coverage artifacts detected: $($coverageState.has_coverage_artifacts)")
if ($coverageState.artifact_names.Count -gt 0) {
    $markdownLines.Add("- Coverage artifact names: $($coverageState.artifact_names -join ', ')")
} else {
    $markdownLines.Add("- Coverage artifact names: none")
}
$markdownLines.Add("- Latest coverage age (days): $($coverageState.latest_upload_days)")
$markdownLines.Add("")
$markdownLines.Add("## Documentation and license checks")
$markdownLines.Add("- Third party notices present: $($licenseState.third_party_notices_present)")
$markdownLines.Add("- API SPDX ID: $($licenseState.spdx_id)")
$markdownLines.Add("- Local license file present: $($licenseState.local_license_present)")
$markdownLines.Add("- Local license pattern match: $($licenseState.local_license_spdx_like)")
$markdownLines.Add("")
$markdownLines.Add("## Dead link validation")
$markdownLines.Add("- Markdown files scanned: $($documentationChecks.files_scanned)")
$markdownLines.Add("- Links scanned: $($documentationChecks.links_scanned)")
$markdownLines.Add("- Remote links checked: $($documentationChecks.remote_links_checked)")
$markdownLines.Add("- Actionable link findings: $($documentationChecks.actionable_count)")
$markdownLines.Add("- Warning findings: $($documentationChecks.warning_count)")
if (($documentationChecks.findings | Measure-Object).Count -gt 0) {
    $markdownLines.Add("")
    $markdownLines.Add("### Top documentation link findings")
    foreach ($finding in $documentationChecks.findings | Select-Object -First 20) {
        $findingLine = "- [$($finding.severity)] $($finding.file): $($finding.url)"
        if ($finding.reason) { $findingLine += " ($($finding.reason))" }
        $markdownLines.Add($findingLine)
    }
}
$markdownLines.Add("")
$markdownLines.Add("## Actionable maintenance items")
if ($actionableItems.Count -eq 0) {
    $markdownLines.Add("- None")
} else {
    foreach ($item in $actionableItems) {
        $markdownLines.Add("- $item")
    }
}
$markdownLines.Add("")
$markdownLines.Add("## Informational maintenance items")
if ($informationalItems.Count -eq 0) {
    $markdownLines.Add("- None")
} else {
    foreach ($item in $informationalItems) {
        $markdownLines.Add("- $item")
    }
}

if ($maintenanceIssue -and $maintenanceIssue.PSObject.Properties.Name -contains "status" -and $maintenanceIssue.status -ne "not_triggered") {
    $markdownLines.Add("")
    $markdownLines.Add("## Maintenance issue action")
    $markdownLines.Add("- Issue automation status: $($maintenanceIssue.status)")
    if ($maintenanceIssue.issue_url) {
        $markdownLines.Add("- Issue link: $($maintenanceIssue.issue_url)")
    }
    if ($maintenanceIssue.error) {
        $markdownLines.Add("- Issue automation error: $($maintenanceIssue.error)")
    }
}

$markdown = $markdownLines -join "`n"
Set-Content -Path (Join-Path $repoRoot.Path $OutputMarkdown) -Value $markdown -Encoding UTF8
$json = $report | ConvertTo-Json -Depth 20
Set-Content -Path (Join-Path $repoRoot.Path $OutputJson) -Value $json -Encoding UTF8

Add-TaskLog "report-write" "passed" "Report artifacts written."
Write-Output "Report written to $OutputMarkdown and $OutputJson"

if ($actionableItems.Count -gt 0) {
    Add-TaskLog "final-status" "failed" "$($actionableItems.Count) actionable item(s) detected."
    if ($isScheduledRun) {
        throw "Weekly OSS maintenance detected $($actionableItems.Count) actionable item(s)."
    } else {
        Write-Output "Actionable findings detected: $($actionableItems.Count)."
    }
} else {
    Add-TaskLog "final-status" "passed" "No actionable maintenance items."
}
