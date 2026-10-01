param([string]$GameManaged,[string]$InstallTo)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$arguments=@('build',(Join-Path $root 'EcologyCulture.csproj'),'-c','Release','-v:q','-clp:ErrorsOnly')
if($GameManaged){$arguments+="-p:GameManaged=$GameManaged"}
& dotnet @arguments
if($LASTEXITCODE -ne 0){throw 'Build failed'}
$package=Join-Path $root 'dist/BaiyeEcologyCulture'
New-Item -ItemType Directory -Path $package -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'bin/Release/netstandard2.1/BaiyeEcologyCulture.dll') -Destination $package -Force
foreach($file in @('mod.yaml','mod_info.yaml','README.md','CHANGELOG.md')){Copy-Item -LiteralPath (Join-Path $root $file) -Destination $package -Force}
foreach($dir in Get-ChildItem -LiteralPath (Join-Path $root 'anim/assets') -Directory){
    $dest=Join-Path $package ('anim/assets/'+$dir.Name)
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Get-ChildItem -LiteralPath $dir.FullName -File | ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $dest -Force}
}
$dlls=Get-ChildItem -LiteralPath $package -Recurse -Filter '*.dll'
if($dlls.Count -ne 1 -or $dlls[0].Name -ne 'BaiyeEcologyCulture.dll'){throw 'Unexpected dependency in package'}
$versionLine=Get-Content -LiteralPath (Join-Path $root 'mod_info.yaml') | Where-Object {$_ -match '^version:'} | Select-Object -First 1
$version=($versionLine -replace '^version:\s*','').Trim()
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid package version'}
Compress-Archive -LiteralPath $package -DestinationPath (Join-Path $root "dist/BaiyeEcologyCulture-$version.zip") -Force
if($InstallTo){
    if(Get-Process -Name OxygenNotIncluded -ErrorAction SilentlyContinue){throw 'Exit Oxygen Not Included before installation'}
    if(Test-Path -LiteralPath $InstallTo){
        # A backup below mods/local would be discovered as another installed
        # mod with the same static ID. Keep it under the project's ignored dist.
        $backupRoot=Join-Path $root 'dist/install-backups'
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        $backup=Join-Path $backupRoot ((Split-Path -Leaf $InstallTo)+'-'+(Get-Date -Format yyyyMMdd-HHmmss))
        Copy-Item -LiteralPath $InstallTo -Destination $backup -Recurse
        Write-Output "Backup $backup"
    }
    New-Item -ItemType Directory -Path $InstallTo -Force | Out-Null
    Get-ChildItem -LiteralPath $package | ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $InstallTo -Recurse -Force}
    $prefix=$package.TrimEnd([char[]]'\/')+[IO.Path]::DirectorySeparatorChar
    foreach($file in Get-ChildItem -LiteralPath $package -Recurse -File){
        $relative=$file.FullName.Substring($prefix.Length)
        $installed=Join-Path $InstallTo $relative
        if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $installed).Hash){throw "Installation hash mismatch: $relative"}
    }
    Write-Output "Installed $InstallTo"
}
Write-Output "Package $package"
