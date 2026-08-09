param(
    [string]$Owner = "ipricetyler-spec",
    [string]$Repo = "open-measurement-evidence-toolkit",
    [ValidateSet("public", "private")]
    [string]$Visibility = "public",
    [string]$MainBranch = "main",
    [string]$Token = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $Token) {
    $Token = [Environment]::GetEnvironmentVariable("GITHUB_TOKEN")
}

$headers = @{ "Accept" = "application/vnd.github+json"; "User-Agent" = "codex-publisher" }
if ($Token) {
    $headers["Authorization"] = "Bearer $Token"
}

$repoPayload = @{
    name = $Repo
    description = "Offline-first .NET toolkit for reproducible measurement evidence validation."
    private = ($Visibility -eq "private")
    visibility = $Visibility
    has_issues = $true
    has_projects = $false
    has_wiki = $false
} | ConvertTo-Json

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Uri,
        [string]$Body = ""
    )
    $params = @{
        Uri = $Uri
        Method = $Method
        Headers = $headers
        ContentType = "application/json"
        ErrorAction = "Stop"
    }
    if ($Body) { $params.Body = $Body }
    return Invoke-RestMethod @params
}

function Ensure-GitHubToken {
    if (-not $Token) {
        throw "No token found. Set `$env:GITHUB_TOKEN or pass -Token."
    }
}

try {
    $apiBase = "https://api.github.com"
    $createUri = "$apiBase/user/repos"
    $repoUrl = "https://github.com/$Owner/$Repo"

    if (-not $Token) {
        Write-Host "No GITHUB_TOKEN detected; attempting `gh` fallback."
        if (Get-Command gh -ErrorAction SilentlyContinue) {
            if ($Visibility -eq "private") {
                gh repo create "$Owner/$Repo" --private --source . --remote origin --push
            } else {
                gh repo create "$Owner/$Repo" --public --source . --remote origin --push
            }
        } else {
            throw "No token and no `gh` CLI installed. Cannot create repository automatically."
        }
    } else {
        # Try to create via API; if it already exists, continue as idempotent path.
        try {
            $createResponse = Invoke-Api -Method "POST" -Uri $createUri -Body $repoPayload
            Write-Host "Created repository: $($createResponse.html_url)"
        } catch {
            if ($_.Exception.Response -and $_.Exception.Response.StatusCode -eq 422) {
                Write-Host "Repository exists; continuing to push to existing remote."
            } else {
                throw
            }
        }

        $repoResp = Invoke-Api -Method "GET" -Uri "$apiBase/repos/$Owner/$Repo"
        if (-not $repoResp.full_name) {
            throw "Could not confirm repository $Owner/$Repo."
        }
        $cloneUrl = $repoResp.clone_url

        if (-not (git remote | Where-Object { $_ -eq "origin" })) {
            git remote add origin $cloneUrl
        } else {
            git remote set-url origin $cloneUrl
        }

        git push -u origin $MainBranch
        Write-Host "Pushed local main branch to $cloneUrl"
    }
} finally {
    Write-Host "Done."
}
