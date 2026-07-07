<#
================================================================================
 Third Party License Collector (NuGet → GitHub LICENSE extractor)
--------------------------------------------------------------------------------
機能:
- csprojからNuGet依存関係を取得
- NuGet metadataからGitHub repositoryを特定
- GitHubからデフォルトブランチを自動判定しLICENSEファイルを収集
- Markdown or 個別ファイルに出力
- System / Microsoft系はデフォルト除外

出力モード:
- single: 1つのMarkdownにまとめる
- split : パッケージごとにLICENSEファイルを出力
================================================================================ #>

param(
    # 対象csproj（未指定ならカレントから自動検出）
    [string]$CsprojPath = "../IMV.csproj",
    
    # singleモード時の出力ファイル
    [string]$OutputFile = "..\THIRD_PARTY_LICENSES.md",
    
    # splitモード時の出力ディレクトリ
    [string]$OutputDir = "..\third-party-licenses",

    # GitHub APIトークン（レート制限対策）
    [string]$GitHubToken = $env:GITHUB_TOKEN,

    # 除外するパッケージプレフィックス
    [string[]]$ExcludePrefixes = @(
        "System.",
        "Microsoft.",
        "runtime.",
        "NETStandard.",
        "Microsoft.NETCore.",
        "Microsoft.AspNetCore."
    )
)

# ================================
# 出力モード
# ================================
# $OutputMode = "single"
$OutputMode = "split"

# ================================
# GitHub API Header
# ================================
$Headers = @{
    "Accept" = "application/vnd.github.v3+json"
}
if ($GitHubToken) {
    $Headers.Add("Authorization", "token $GitHubToken")
}

# ================================
# csproj resolve
# ================================
function Resolve-CsprojPath {
    param([string]$path)

    if ($path -and $path.Trim() -ne "") {
        return $path
    }

    $csprojFiles = Get-ChildItem -Path . -Filter *.csproj -File

    if ($csprojFiles.Count -eq 0) {
        throw "No .csproj file found."
    }

    return $csprojFiles[0].FullName
}

# ================================
# Package refs
# ================================
function Get-PackageRefs {
    param([string]$csproj)

    [xml]$xml = Get-Content $csproj
    $items = $xml.SelectNodes("//PackageReference")

    foreach ($i in $items) {
        [PSCustomObject]@{
            Id = $i.Include
            Version = $i.Version
        }
    }
}

# ================================
# exclude
# ================================
function Should-ExcludePackage {
    param([string]$id)

    foreach ($p in $ExcludePrefixes) {
        if ($id.StartsWith($p)) {
            return $true
        }
    }
    return $false
}

# ================================
# NuGet metadata
# ================================
function Get-NuGetMetadata {
    param($id, $version)

    $nuspecUrl = "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.nuspec"

    try {
        Invoke-RestMethod $nuspecUrl -ErrorAction Stop
    } catch {
        $null
    }
}

# ================================
# GitHub repo & Default Branch
# ================================
function Get-GitHubRepoInfo {
    param($nuspec)

    if (-not $nuspec) { return $null }

    try {
        $repoUrl = $nuspec.package.metadata.repository.url
        if ($repoUrl -and $repoUrl -match "github.com/(?<owner>[^/]+)/(?<repo>[^/.]+)") {
            $owner = $Matches.owner
            $repo = $Matches.repo
            
            # APIからデフォルトブランチを取得
            $apiUrl = "https://api.github.com/repos/$owner/$repo"
            $repoData = Invoke-RestMethod $apiUrl -Headers $Headers -ErrorAction SilentlyContinue
            
            return [PSCustomObject]@{
                Path = "$owner/$repo"
                DefaultBranch = if ($repoData) { $repoData.default_branch } else { "master" }
            }
        }
    } catch {}

    return $null
}

# ================================
# LICENSE fetch
# ================================
function Get-GitHubLicense {
    param($repoInfo)
    if (-not $repoInfo) { return $null }

    # 1. APIを使ってリポジトリルートのファイル一覧を取得
    $treeUrl = "https://api.github.com/repos/$($repoInfo.Path)/contents/?ref=$($repoInfo.DefaultBranch)"
    try {
        $files = Invoke-RestMethod $treeUrl -Headers $Headers -ErrorAction Stop
        
        # 2. ライセンスファイルに該当しそうな名前を探す（大文字小文字を区別せずマッチ）
        $licenseFile = $files | Where-Object { 
            $_.name -match "^(LICENSE|COPYING)(\.md|\.txt|\.text|)$" 
        } | Select-Object -First 1

        if ($null -ne $licenseFile) {
            Write-Host "  [i] Found license file: $($licenseFile.name)" -ForegroundColor Cyan
            # 3. 特定したファイルパスにアクセス
            return Invoke-RestMethod $licenseFile.download_url -ErrorAction Stop
        }
    } catch {
        Write-Host "  [!] Failed to list files via API: $($repoInfo.Path)" -ForegroundColor Yellow
    }

    return $null
}

# ================================
# main
# ================================
$CsprojPath = Resolve-CsprojPath $CsprojPath
Write-Host "Using csproj: $CsprojPath"

$packages = Get-PackageRefs $CsprojPath | Sort-Object Id -Unique

$result = @()

foreach ($pkg in $packages) {

    if (Should-ExcludePackage $pkg.Id) {
        Write-Host "Skipping $($pkg.Id)"
        continue
    }

    Write-Host "Processing $($pkg.Id) $($pkg.Version)..."

    $nuspec = Get-NuGetMetadata $pkg.Id $pkg.Version
    $repoInfo = Get-GitHubRepoInfo $nuspec
    $license = Get-GitHubLicense $repoInfo

    $result += [PSCustomObject]@{
        Package = $pkg.Id
        Version = $pkg.Version
        Repo    = "https://github.com/$($repoInfo.Path)"
        License = $license
    }
}

# ================================
# OUTPUT
# ================================
if ($OutputMode -eq "single") {

    $sb = New-Object System.Text.StringBuilder

    [void]$sb.AppendLine("# Third Party Licenses")
    [void]$sb.AppendLine("")

    foreach ($r in $result) {

        [void]$sb.AppendLine("## $($r.Package) $($r.Version)")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("Repo: $($r.Repo)")
        [void]$sb.AppendLine("")

        [void]$sb.AppendLine('```text')
        if ($r.License) {
            [void]$sb.AppendLine($r.License)
        } else {
            [void]$sb.AppendLine("LICENSE NOT FOUND")
        }
        [void]$sb.AppendLine('```')
        [void]$sb.AppendLine("")
    }

    Set-Content -Path $OutputFile -Value $sb.ToString() -Encoding UTF8
    Write-Host "Output -> $OutputFile"
}

elseif ($OutputMode -eq "split") {

    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir | Out-Null
    }

    foreach ($r in $result) {

        $safeName = ($r.Package -replace "[^a-zA-Z0-9_\-\.]", "_")
        $filePath = Join-Path $OutputDir "$safeName`_license.txt"

        $content = if ($r.License) { $r.License } else { "LICENSE NOT FOUND" }

        Set-Content $filePath $content -Encoding UTF8

        Write-Host "Wrote $filePath"
    }

    Write-Host "Output dir -> $OutputDir"
}