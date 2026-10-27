param([string]$CacheDirectory = (Join-Path $PSScriptRoot '../.bundles'))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $CacheDirectory -Force | Out-Null
$components = @(
    @{Name='millennium.zip';Url='https://github.com/SteamClientHomebrew/Millennium/releases/download/v3.6.2/millennium-v3.6.2-windows-x86_64.zip';Hash='CD78CF680563491D24F58A4FC948DCFC7CB3B38523AC80162845E52B06151CC2'},
    @{Name='nativegamelink-upstream.zip';Url='https://github.com/senkumil/NativeGameLink-for-Steam/releases/download/V5.0.0/NativeGameLinkForSteam-v5.0.0.zip';Hash='80F8C9C14B4979196DF4D565F9C0488CEDF8C124198A28D1E7D181051D2006CE'}
)
foreach($component in $components) {
    $path = Join-Path $CacheDirectory $component.Name
    if(!(Test-Path -LiteralPath $path)) {Invoke-WebRequest -Uri $component.Url -OutFile $path}
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $component.Hash) {throw "Component hash mismatch: $($component.Name)"}
}
$source = [IO.Compression.ZipFile]::OpenRead((Join-Path $CacheDirectory 'nativegamelink-upstream.zip'))
$targetPath = Join-Path $CacheDirectory 'library-details.zip'
$targetStream = [IO.File]::Create($targetPath)
$target = [IO.Compression.ZipArchive]::new($targetStream,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($entry in $source.Entries) {
        if($entry.FullName.EndsWith('/')) {continue}
        if($entry.FullName -match '/backend/src/(steam_achievement_sync|steam_card_farmer)\.ps1$') {continue}
        $reader = [IO.StreamReader]::new($entry.Open())
        try {$text = $reader.ReadToEnd()} finally {$reader.Dispose()}
        if($entry.FullName.EndsWith('/.millennium/Dist/index.js')) {
            $tokens = [regex]::Matches($text, '["''][a-f0-9]{28,40}["'']')
            if($tokens.Count -ne 2) {throw 'Pinned frontend credential pattern changed; review distribution patches.'}
            $text = [regex]::Replace($text, '["''][a-f0-9]{28,40}["'']', '""')
        }
        if($entry.FullName.EndsWith('/backend/main.lua')) {
            $policy = @"
-- UWPHook distribution: metadata integration never writes account achievements
-- or starts farming tools, and never changes game licensing files or elevation.
local function uwphook_read_only()
    return cjson.encode({ ok = false, error = 'disabled_in_uwphook_distribution' })
end
sync_steam_account_achievements = uwphook_read_only
start_steam_card_farming = uwphook_read_only
suppress_admin_prompt = uwphook_read_only
neutralize_steam_appid_file = uwphook_read_only
restore_steam_appid_file = uwphook_read_only

"@
            if(!$text.Contains('return {' + "`r`n") -and !$text.Contains('return {' + "`n")) {throw 'Backend entry point format changed'}
            $lastReturn = $text.LastIndexOf('return {')
            $text = $text.Insert($lastReturn,$policy)
        }
        $newEntry = $target.CreateEntry($entry.FullName,[IO.Compression.CompressionLevel]::Optimal)
        $newEntry.LastWriteTime = [DateTimeOffset]::new(2026,10,23,0,0,0,[TimeSpan]::Zero)
        $writer = [IO.StreamWriter]::new($newEntry.Open(),[Text.UTF8Encoding]::new($false))
        try {$writer.Write($text)} finally {$writer.Dispose()}
    }
} finally {$source.Dispose();$target.Dispose();$targetStream.Dispose()}
@{HostVersion='3.6.2';PluginVersion='5.0.0-uwphook.1';HostHash=$components[0].Hash;PluginHash=(Get-FileHash -LiteralPath $targetPath).Hash} | ConvertTo-Json | Set-Content (Join-Path $CacheDirectory 'components.json') -Encoding utf8
Write-Output 'Pinned library details components prepared and verified.'
