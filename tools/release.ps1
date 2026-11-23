<#
.SYNOPSIS
  DEFLATE Custom Chart 모드 릴리즈 스크립트 (게임이 설치된 PC에서 실행 — 빌드에 게임 interop DLL이 필요).

.DESCRIPTION
  1) 버전 일치 확인: DeflateMod.Version = CHANGELOG.md 최상단 ## [버전] = AssemblyInfo AssemblyVersion(앞 3자리)
  2) dotnet test (tests/, 게임 불필요) → dotnet build -c Release (경고 0 / 오류 0이어야 통과)
  3) dist\DEFLATE-custom-chart-v<버전>.zip (DLL 하나) + SHA256 + 릴리즈 노트 초안 생성
  -Publish: main이 origin/main과 같고 작업 트리가 깨끗한지 확인 → 태그 v<버전> 푸시 → gh release create

.EXAMPLE
  .\tools\release.ps1
  .\tools\release.ps1 -Publish
  .\tools\release.ps1 -GameDir "D:\SteamLibrary\steamapps\common\DEFLATE\"
#>
param(
    [switch]$Publish,
    [string]$GameDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$proj = Join-Path $root "DEFLATE custom chart\DEFLATE custom chart.csproj"
$modCs = Join-Path $root "DEFLATE custom chart\Core\DeflateMod.cs"
$asmInfo = Join-Path $root "DEFLATE custom chart\Properties\AssemblyInfo.cs"
$changelog = Join-Path $root "CHANGELOG.md"

# ── 1. 버전 일치 ──
$modVer = ([regex]::Match((Get-Content $modCs -Raw), 'const string Version = "([0-9.]+)"')).Groups[1].Value
$logVer = ([regex]::Match((Get-Content $changelog -Raw), '(?m)^## \[([0-9.]+)\]')).Groups[1].Value
$asmVer = ([regex]::Match((Get-Content $asmInfo -Raw), 'AssemblyVersion\("([0-9]+\.[0-9]+\.[0-9]+)')).Groups[1].Value
if (-not $modVer -or $modVer -ne $logVer -or $modVer -ne $asmVer) {
    throw "버전 불일치: DeflateMod.Version=$modVer, CHANGELOG=$logVer, AssemblyInfo=$asmVer"
}
$tag = "v$modVer"
Write-Host "버전 $modVer 확인 (DeflateMod / CHANGELOG / AssemblyInfo 일치)"

# ── 2a. 오프라인 단위 테스트 (게임 불필요, CI와 같은 테스트) ──
$testProj = Join-Path $root "tests\DEFLATE.CustomChart.Tests\DEFLATE.CustomChart.Tests.csproj"
$testOut = & dotnet test $testProj -c Release -nologo 2>&1
$testOut | Write-Host
if ($LASTEXITCODE -ne 0) { throw "단위 테스트 실패" }

# ── 2. 빌드 ──
# -warnaserror: AGENTS.md §5-7 원칙(경고 0 / 오류 0)을 릴리즈 관문으로 강제
$buildArgs = @("build", $proj, "-c", "Release", "-nologo", "-warnaserror")
if ($GameDir) { $buildArgs += "-p:GameDir=$GameDir" }
$buildOut = & dotnet @buildArgs 2>&1
$buildOut | Write-Host
if ($LASTEXITCODE -ne 0) { throw "빌드 실패" }
$dll = Join-Path $root "DEFLATE custom chart\bin\Release\net6.0\DEFLATE custom chart.dll"
if (-not (Test-Path $dll)) { throw "DLL을 찾을 수 없습니다: $dll" }

# ── 3. zip + 노트 ──
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip = Join-Path $dist "DEFLATE-custom-chart-$tag.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $dll -DestinationPath $zip
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
$dllSha = (Get-FileHash $dll -Algorithm SHA256).Hash.ToLower()

$section = [regex]::Match((Get-Content $changelog -Raw), "(?ms)^## \[$([regex]::Escape($modVer))\][^\n]*\n(.*?)(?=^## \[|\z)").Groups[1].Value.Trim()
$notes = @"
$section

## 설치
1. DEFLATE에 MelonLoader **0.7.3**을 설치하고 게임을 한 번 실행합니다.
2. zip 안의 ``DEFLATE custom chart.dll``을 ``<게임 폴더>\Mods\``에 넣습니다.
3. 커스텀 곡을 ``<게임 폴더>\hwa\``에 넣습니다(README 참고). 게임은 Steam으로 실행하세요.
4. ``MelonLoader\Latest.log``에서 ``[GameCompat] … → 일치``와 ``[SafePatcher] … 실패 0``을 확인하세요.

## SHA256
- ``DEFLATE-custom-chart-$tag.zip``: ``$sha``
- ``DEFLATE custom chart.dll``: ``$dllSha`` (``Latest.log``의 ``SHA256 Hash:``와 비교)
"@
$notesFile = Join-Path $dist "release-notes-$tag.md"
Set-Content -Path $notesFile -Value $notes -Encoding utf8
Write-Host "생성: $zip"
Write-Host "생성: $notesFile"

if (-not $Publish) { return }

# ── 4. 게시 ──
# git/gh는 정상 진행 메시지("From https://…", "To https://…")를 stderr로 쓴다. Windows PowerShell 5.1에서
# ErrorActionPreference=Stop이면 그 stderr 줄이 종료 오류가 되어 게시가 중간에 멈추므로(2026-11-23 v1.1.2 릴리즈 때 발생),
# 이 구간은 Continue로 두고 성공 여부는 종료 코드로만 판정한다.
$ErrorActionPreference = "Continue"
function Invoke-Native([string]$what, [scriptblock]$cmd) {
    & $cmd 2>&1 | ForEach-Object { "$_" } | Write-Host
    if ($LASTEXITCODE -ne 0) { throw "$what 실패 (종료 코드 $LASTEXITCODE)" }
}

Invoke-Native "git fetch" { git fetch origin main }
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne "main") { throw "main 브랜치에서 실행하세요 (현재 $branch)" }
if ((git rev-parse HEAD).Trim() -ne (git rev-parse origin/main).Trim()) { throw "main이 origin/main과 다릅니다. pull/push 후 다시 실행하세요." }
if (git status --porcelain --untracked-files=no) { throw "작업 트리가 깨끗하지 않습니다." }
if (git tag --list $tag) { throw "태그 $tag 가 이미 있습니다. 세 곳의 버전을 먼저 올리세요." }

Invoke-Native "git tag" { git tag -a $tag -m "DEFLATE Custom Chart $tag" }
Invoke-Native "git push (태그)" { git push origin $tag }
Invoke-Native "gh release create" { gh release create $tag $zip --title "DEFLATE Custom Chart $tag" --notes-file $notesFile }
Write-Host "게시 완료: $tag"
