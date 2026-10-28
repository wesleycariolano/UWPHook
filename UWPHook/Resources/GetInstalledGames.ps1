$ErrorActionPreference = 'Stop'
$result = [Collections.Generic.List[object]]::new()
foreach ($package in @(Get-AppxPackage)) {
    if ($package.IsFramework -or $package.IsResourcePackage -or !$package.InstallLocation) { continue }
    try {
        $manifest = Get-AppxPackageManifest $package
        $configPath = Join-Path $package.InstallLocation 'MicrosoftGame.Config'
        $hasConfig = Test-Path -LiteralPath $configPath
        $config = $null
        if ($hasConfig) { try { [xml]$config = Get-Content -LiteralPath $configPath -Raw } catch {} }
        $explorer = $null -ne $manifest.SelectSingleNode("//*[local-name()='GameExplorer']")
        foreach ($application in @($manifest.Package.Applications.Application)) {
            if (!$application.Id) { continue }
            $exe = [string]$application.Executable
            $helper = $exe -ieq 'GameLaunchHelper.exe'
            if ($config -and ($helper -or [string]::IsNullOrWhiteSpace($exe))) {
                $choices = @($config.Game.ExecutableList.Executable)
                $choice = @($choices | Where-Object { $_.Id -eq $application.Id } | Select-Object -First 1)
                if ($choice.Count -eq 0) { $choice = @($choices | Select-Object -First 1) }
                if ($choice.Count -gt 0) { $exe = [string]$choice[0].Name }
            }
            $name = [string]$application.VisualElements.DisplayName
            if (!$name -or $name -like 'ms-resource:*') { $name = [string]$manifest.Package.Properties.DisplayName }
            if ((!$name -or $name -like 'ms-resource:*') -and $config) { $name = [string]$config.Game.ShellVisuals.DefaultDisplayName }
            if (!$name -or $name -like 'ms-resource:*') { $name = $package.Name }
            $logo = [string]$application.VisualElements.Square150x150Logo
            $icon = if ($logo) { Join-Path $package.InstallLocation $logo } else { '' }
            $result.Add([pscustomobject]@{
                Name=$name; Aumid=($package.PackageFamilyName+'!'+$application.Id); Executable=$exe
                PackageName=$package.Name; InstallDirectory=$package.InstallLocation; Icon=$icon
                IconPath=if($icon){Split-Path -Parent $icon}else{''}
                HasGameConfig=$hasConfig; HasGameExplorer=$explorer; UsesGameLaunchHelper=$helper
            })
        }
    } catch { continue }
}
ConvertTo-Json -InputObject @($result.ToArray()) -Depth 4 -Compress
