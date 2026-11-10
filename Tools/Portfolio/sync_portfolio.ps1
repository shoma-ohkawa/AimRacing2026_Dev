# Portfolio: AimRacing2026_Dev のコミット済みの状態(HEAD)から、ポートフォリオ用リポジトリへソースを書き出す。
# どの PC でも同じ結果になる。push と、GitHub のリポジトリ・Release の作成はしない(最後に手順を出す)。
#   .\sync_portfolio.ps1                                     書き出し先 = このリポジトリの 1 つ上の AimRacing2026_Portfolio
#   .\sync_portfolio.ps1 -Target E:\AimRacing\AimRacing2026_Portfolio
#   .\sync_portfolio.ps1 -Commit                             書き出したあと、差分があればコミットする
# 書き出すファイルは manifest.txt で決める。作業ツリーの未コミットの変更は書き出さない(HEAD の内容を書き出す)。
param(
  [string]$Target = '',        # ポートフォリオ用リポジトリの場所。省略すると、このリポジトリの 1 つ上の AimRacing2026_Portfolio
  [string]$Remote = 'https://github.com/shoma-ohkawa/AimRacing2026_Portfolio.git',  # GitHub 側の場所
  [string]$AuthorName = '大川翔生',   # 新しく作ったリポジトリにだけ設定するコミットの作成者名(メールは各 PC の設定のまま)
  [switch]$Commit,             # 書き出したあと、差分があればコミットする
  [string]$Message = ''        # コミットメッセージ。省略すると「AimRacing2026_Dev <hash> から同期」
)

$ErrorActionPreference = 'Stop'
$devRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$srcPrefix = 'Assets/#Scripts/'             # Dev 側のソースの置き場。書き出し先では Source/ になる
$mirrorRoots = @('Source', 'Tools')         # 書き出し先で、マニフェストにないファイルを消すフォルダ

# git を実行し、失敗したら止める。出力(行の配列)を返す
function Invoke-Git([string]$dir, [string[]]$gitArgs) {
  $out = & git -C $dir -c core.quotepath=false @gitArgs
  if ($LASTEXITCODE -ne 0) { throw "git $($gitArgs -join ' ') が失敗しました(終了コード $LASTEXITCODE): $dir" }
  return $out
}

# git を実行し、終了コードだけ返す(失敗しても止めない。接続の確認用)
function Get-GitExit([string]$dir, [string[]]$gitArgs) {
  $old = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try { & git -C $dir @gitArgs 2>&1 | Out-Null } finally { $ErrorActionPreference = $old }
  return $LASTEXITCODE
}

# GitHub 側の状態を返す。'has-main' = main がある、'empty' = リポジトリはあるが main がない、'none' = 接続できない(未作成・非公開・認証なし)
function Get-RemoteState([string]$dir) {
  $code = Get-GitExit $dir @('ls-remote', '--exit-code', '--heads', 'origin', 'main')   # exit 2 = main がない
  if ($code -eq 0) { return 'has-main' }
  if ($code -eq 2) { return 'empty' }
  return 'none'
}

# Dev のパスから、書き出し先の相対パスを返す
function Get-DestRel([string]$devPath) {
  if ($devPath.StartsWith($srcPrefix)) { return 'Source/' + $devPath.Substring($srcPrefix.Length) }
  return $devPath
}

# --- 1. マニフェストを読む
if ((Get-GitExit $devRoot @('rev-parse', '--git-dir')) -ne 0) { throw "git のリポジトリではありません: $devRoot" }
$devHash = (Invoke-Git $devRoot @('rev-parse', '--short', 'HEAD')) | Select-Object -First 1
$entries = Get-Content (Join-Path $PSScriptRoot 'manifest.txt') -Encoding UTF8 |
  ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' -and -not $_.StartsWith('#') }
$files = New-Object System.Collections.Generic.List[string]
foreach ($entry in $entries) {
  if ($entry.EndsWith('/')) {
    $listed = @(Invoke-Git $devRoot @('ls-tree', '-r', '--name-only', 'HEAD', '--', $entry))
    if ($listed.Count -eq 0) { throw "マニフェストのフォルダに、追跡中のファイルがありません: $entry" }
    foreach ($item in $listed) { $files.Add($item) }
  } else {
    if ((Get-GitExit $devRoot @('cat-file', '-e', "HEAD:$entry")) -ne 0) { throw "マニフェストのファイルが Dev の HEAD にありません: $entry" }
    $files.Add($entry)
  }
}
$keep = @{}
foreach ($f in $files) {
  $dest = Get-DestRel $f
  $top = $dest.Split('/')[0]
  if ($mirrorRoots -notcontains $top) { throw "書き出し先が Source/ か Tools/ の外になります: $f -> $dest" }
  $keep[$dest] = $true
}
Write-Host "書き出すファイル: $($files.Count) 個(Dev の HEAD = $devHash)"

# Dev で未コミットの変更があるファイルは、HEAD の内容で書き出される。知らせておく
$dirty = @(Invoke-Git $devRoot (@('status', '--porcelain', '--') + $files.ToArray()))
if ($dirty.Count -gt 0) {
  Write-Host '注意: Dev で未コミットの変更があるファイルは、コミット済みの内容で書き出します。変更を含めるなら、先に Dev でコミットしてください。' -ForegroundColor Yellow
  $dirty | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
}

# --- 2. 書き出し先のリポジトリを用意する
if ($Target -eq '') { $Target = Join-Path (Split-Path $devRoot -Parent) 'AimRacing2026_Portfolio' }
$Target = [IO.Path]::GetFullPath($Target).TrimEnd('\')
$env:GIT_TERMINAL_PROMPT = '0'      # 接続の確認で、認証を聞かない
$env:GCM_INTERACTIVE = 'never'
$remoteState = ''                   # 'has-main'(GitHub に main がある) / 'empty'(リポジトリはあるが main がない) / 'none'(接続できない)

if (-not (Test-Path (Join-Path $Target '.git'))) {
  Write-Host "新しくリポジトリを用意します: $Target"
  New-Item -ItemType Directory -Force $Target | Out-Null
  Invoke-Git $Target @('init', '-b', 'main') | Out-Null
  Invoke-Git $Target @('remote', 'add', 'origin', $Remote) | Out-Null
  Invoke-Git $Target @('config', 'user.name', $AuthorName) | Out-Null
  $remoteState = Get-RemoteState $Target
  if ($remoteState -eq 'has-main') {
    # GitHub の main を履歴として取り込む。作業ツリーのファイルはそのまま残す(無いファイルだけ戻す)
    Invoke-Git $Target @('fetch', 'origin', 'main') | Out-Null
    Invoke-Git $Target @('reset', 'origin/main') | Out-Null
    Invoke-Git $Target @('branch', '--set-upstream-to=origin/main', 'main') | Out-Null
    $missing = @(Invoke-Git $Target @('ls-files', '-d'))
    if ($missing.Count -gt 0) { Invoke-Git $Target (@('checkout', '--') + $missing) | Out-Null }
    Write-Host 'GitHub の main の履歴を取り込みました。'
  }
} else {
  # 既にあるリポジトリ。GitHub に新しいコミットがあれば止める(別の PC で push した分を取り込んでから同期する)
  $remoteState = Get-RemoteState $Target
  if ($remoteState -eq 'has-main') { Invoke-Git $Target @('fetch', 'origin', 'main') | Out-Null }
  if ((Get-GitExit $Target @('rev-parse', '--abbrev-ref', '@{u}')) -eq 0) {
    $behind = [int](Invoke-Git $Target @('rev-list', '--count', 'HEAD..@{u}') | Select-Object -First 1)
    if ($behind -gt 0) { throw "GitHub に新しいコミットが $behind 件あります。先に '$Target' で git pull してから、もう一度実行してください。" }
  }
}

# --- 3. Dev の HEAD から書き出す
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('portfolio_export_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
  $zip = Join-Path $tmp 'export.zip'
  $outDir = Join-Path $tmp 'out'
  Invoke-Git $devRoot (@('archive', '--format=zip', '-o', $zip, 'HEAD', '--') + $files.ToArray()) | Out-Null
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  [IO.Compression.ZipFile]::ExtractToDirectory($zip, $outDir)
  foreach ($f in $files) {
    $from = Join-Path $outDir $f.Replace('/', '\')
    $to = Join-Path $Target (Get-DestRel $f).Replace('/', '\')
    New-Item -ItemType Directory -Force (Split-Path $to -Parent) | Out-Null
    Copy-Item $from $to -Force
  }
} finally {
  Remove-Item $tmp -Recurse -Force
}

# マニフェストにないファイルを、Source/・Tools/ から消す(git で戻せる)
foreach ($root in $mirrorRoots) {
  $dir = Join-Path $Target $root
  if (-not (Test-Path $dir)) { continue }
  Get-ChildItem $dir -Recurse -File -Force | ForEach-Object {
    $rel = $_.FullName.Substring($Target.Length).TrimStart('\').Replace('\', '/')
    if (-not $keep.ContainsKey($rel)) {
      Remove-Item $_.FullName -Force
      Write-Host "削除: $rel"
    }
  }
  Get-ChildItem $dir -Recurse -Directory -Force | Sort-Object FullName -Descending |
    Where-Object { -not (Get-ChildItem $_.FullName -Force) } | Remove-Item -Force
}

# --- 4. 無いときだけ作るファイル(上書きしない。README は手で書き足していく)
$templates = @(
  @('template.gitignore', '.gitignore'),
  @('template.gitattributes', '.gitattributes'),
  @('template.README.md', 'README.md')
)
foreach ($pair in $templates) {
  $to = Join-Path $Target $pair[1]
  if (-not (Test-Path $to)) {
    Copy-Item (Join-Path $PSScriptRoot $pair[0]) $to
    Write-Host "作成: $($pair[1])(雛形)"
  }
}

# --- 5. コミット・結果の表示
if ($Commit) {
  Invoke-Git $Target @('add', '-A') | Out-Null
  $staged = @(Invoke-Git $Target @('status', '--short'))
  if ($staged.Count -eq 0) {
    Write-Host '差分がないので、コミットしません。'
  } else {
    if ($Message -eq '') { $Message = "AimRacing2026_Dev $devHash から同期" }
    Invoke-Git $Target @('commit', '-m', $Message) | Out-Null
    Write-Host "コミットしました: $Message"
  }
}

Write-Host ''
Write-Host "書き出し先: $Target"
$status = @(Invoke-Git $Target @('status', '--short'))
if ($status.Count -eq 0) { Write-Host '作業ツリー: 差分なし' } else { Write-Host "作業ツリー: 差分 $($status.Count) 件"; $status | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" } }

$placeholders = @(Select-String -Path (Join-Path $Target 'README.md') -Pattern '【要確認】|【準備中】')
if ($placeholders.Count -gt 0) { Write-Host "README に【要確認】【準備中】が $($placeholders.Count) 行残っています。公開前に解消してください。" -ForegroundColor Yellow }

Write-Host ''
if ($remoteState -ne 'has-main') {
  Write-Host 'GitHub には、まだ main がありません(未作成・非公開・認証なしのいずれか)。次は手で行います:'
  Write-Host "  1. GitHub で空のリポジトリを作る(README・ライセンスは付けない): $Remote"
  Write-Host "  2. cd `"$Target`" して、git push -u origin main"
  Write-Host '  3. Releases で v1.0.0 を作り、AimRacing2026_Build.zip を添付する'
}
