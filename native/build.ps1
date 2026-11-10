[CmdletBinding()]
param(
    [string]$Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$LibraryName = 'cvnative'
$ClangFlags = @('-O3', '-fwrapv', '-ffp-contract=off', '-fvisibility=hidden')
$MsvcFlags = @('/nologo', '/O2', '/fp:precise', '/W3', '/LD')
$MsvcArchitectures = @{ 'win-x64' = 'x64'; 'win-arm64' = 'x64_arm64' }
$ClangTargets = @{ 'win-x64' = 'x86_64-pc-windows-msvc'; 'win-arm64' = 'aarch64-pc-windows-msvc' }

$NativeRoot = $PSScriptRoot
$Source = Join-Path $NativeRoot 'shim\cvnative.c'
$OutputDirectory = Join-Path $NativeRoot "bin\$Rid"
$ObjectDirectory = Join-Path $NativeRoot "obj\$Rid"

function Assert-SupportedRid {
    if (-not $MsvcArchitectures.ContainsKey($Rid)) {
        throw "build.ps1 builds Windows targets only ($($MsvcArchitectures.Keys -join ', ')). Use build.sh for '$Rid'."
    }
}

function Assert-Sources {
    if (-not (Test-Path (Join-Path $NativeRoot 'cubiomes\generator.c'))) {
        throw "cubiomes sources are missing. Run 'git submodule update --init' in the repository root."
    }
}

function Find-VisualStudio {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        return $null
    }
    return & $vswhere -latest -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
}

function Invoke-Clang {
    $output = Join-Path $OutputDirectory "$LibraryName.dll"
    & clang "--target=$($ClangTargets[$Rid])" @ClangFlags -shared -o $output $Source
    if ($LASTEXITCODE -ne 0) {
        throw "clang failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Msvc {
    param([string]$VisualStudioPath)

    $vcvars = Join-Path $VisualStudioPath 'VC\Auxiliary\Build\vcvarsall.bat'
    $script = Join-Path $ObjectDirectory 'build.cmd'
    $compile = "cl $($MsvcFlags -join ' ') /Fo`"$ObjectDirectory\\`" /Fe`"$OutputDirectory\$LibraryName.dll`" `"$Source`" /link /IMPLIB:`"$ObjectDirectory\$LibraryName.lib`""
    Set-Content -Path $script -Encoding ascii -Value @(
        '@echo off',
        'set "PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer;%PATH%"',
        "call `"$vcvars`" $($MsvcArchitectures[$Rid]) >nul",
        'where cl >nul 2>nul || (echo cl.exe was not found after vcvarsall & exit /b 1)',
        "$compile || exit /b 1"
    )

    & cmd.exe /d /c $script
    if ($LASTEXITCODE -ne 0) {
        throw "MSVC failed with exit code $LASTEXITCODE."
    }
}

Assert-SupportedRid
Assert-Sources
New-Item -ItemType Directory -Force $OutputDirectory, $ObjectDirectory | Out-Null

if (Get-Command clang -ErrorAction SilentlyContinue) {
    Write-Host "Building $LibraryName for $Rid with clang"
    Invoke-Clang
}
else {
    $visualStudio = Find-VisualStudio
    if (-not $visualStudio) {
        throw 'No C compiler found. Install LLVM (clang) or the Visual Studio C++ build tools.'
    }
    Write-Host "Building $LibraryName for $Rid with MSVC ($visualStudio)"
    Invoke-Msvc -VisualStudioPath $visualStudio
}

Write-Host "Built $(Join-Path $OutputDirectory "$LibraryName.dll")"
