[CmdletBinding()]
param(
    [string]$Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$LibraryName = 'cvnative'
$CompileFlags = @('-std=gnu17', '-O3', '-fwrapv', '-ffp-contract=off', '-fvisibility=hidden', '-ffunction-sections', '-fdata-sections', '-DCJSON_HIDE_SYMBOLS')
$ClangTargets = @{ 'win-x64' = 'x86_64-pc-windows-msvc'; 'win-arm64' = 'aarch64-pc-windows-msvc' }
$VisualStudioClangComponent = 'Microsoft.VisualStudio.Component.VC.Llvm.Clang'

$NativeRoot = $PSScriptRoot
$CubiomesRoot = Join-Path $NativeRoot 'cubiomes'
$OutputDirectory = Join-Path $NativeRoot "bin\$Rid"
$ObjectDirectory = Join-Path $NativeRoot "obj\$Rid"
$Output = Join-Path $OutputDirectory "$LibraryName.dll"

function Assert-SupportedRid {
    if (-not $ClangTargets.ContainsKey($Rid)) {
        throw "build.ps1 builds Windows targets only ($($ClangTargets.Keys -join ', ')). Use build.sh for '$Rid'."
    }
}

function Assert-Sources {
    if (-not (Test-Path (Join-Path $CubiomesRoot 'generator.c'))) {
        throw "cubiomes sources are missing. Run 'git submodule update --init' in the repository root."
    }
}

# noise.c is compiled through shim\cv_noise.c; tests.c and tests\ are cubiomes' own test programs.
function Get-CubiomesSources {
    $excluded = @((Join-Path $CubiomesRoot 'noise.c'), (Join-Path $CubiomesRoot 'tests.c'))
    $testsDirectory = Join-Path $CubiomesRoot 'tests\'
    $cubiomes = Get-ChildItem $CubiomesRoot -Recurse -File -Filter '*.c' |
        Where-Object { $excluded -notcontains $_.FullName -and -not $_.FullName.StartsWith($testsDirectory, [StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object FullName
    return @(Join-Path $NativeRoot 'shim\cv_noise.c') + @($cubiomes.FullName)
}

function Test-UpToDate {
    if (-not (Test-Path $Output)) {
        return $false
    }

    $built = (Get-Item $Output).LastWriteTimeUtc
    $sources = @(Get-ChildItem (Join-Path $NativeRoot 'shim') -File -Recurse) +
        @(Get-ChildItem $CubiomesRoot -File -Recurse -Include '*.c', '*.h') +
        @(Get-Item $PSCommandPath)
    return -not ($sources | Where-Object { $_.LastWriteTimeUtc -gt $built })
}

function Find-VisualStudioClang {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        return $null
    }

    $visualStudio = & $vswhere -latest -prerelease -products * -requires $VisualStudioClangComponent -property installationPath
    if (-not $visualStudio) {
        return $null
    }

    $hostArchitecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'ARM64' } else { 'x64' }
    $clang = Join-Path $visualStudio "VC\Tools\Llvm\$hostArchitecture\bin\clang.exe"
    if (Test-Path $clang) {
        return $clang
    }
    return $null
}

function Find-Clang {
    $onPath = Get-Command clang -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    $llvm = Join-Path $env:ProgramFiles 'LLVM\bin\clang.exe'
    if (Test-Path $llvm) {
        return $llvm
    }

    $visualStudioClang = Find-VisualStudioClang
    if ($visualStudioClang) {
        return $visualStudioClang
    }

    throw "clang was not found. Install LLVM, or add 'C++ Clang Compiler for Windows' in the Visual Studio Installer; cubiomes does not build with MSVC."
}

function Assert-ClangSucceeded {
    if ($LASTEXITCODE -ne 0) {
        throw "clang failed with exit code $LASTEXITCODE."
    }
}

# The shim is compiled with warnings on its own; cubiomes is third-party code, compiled and linked without them.
function Invoke-Build {
    $clang = Find-Clang
    Write-Host "Building $LibraryName for $Rid with $clang"

    $target = "--target=$($ClangTargets[$Rid])"
    $shimObject = Join-Path $ObjectDirectory 'cvnative.o'
    & $clang $target @CompileFlags -c -o $shimObject (Join-Path $NativeRoot 'shim\cvnative.c')
    Assert-ClangSucceeded

    $importLibrary = Join-Path $ObjectDirectory "$LibraryName.lib"
    & $clang $target @CompileFlags -w -shared '-Wl,/OPT:REF' "-Wl,/IMPLIB:$importLibrary" -o $Output $shimObject @(Get-CubiomesSources)
    Assert-ClangSucceeded

    Write-Host "Built $Output"
}

Assert-SupportedRid
Assert-Sources
New-Item -ItemType Directory -Force $OutputDirectory, $ObjectDirectory | Out-Null

$lock = [System.Threading.Mutex]::new($false, "Local\cvnative-build-$Rid")
try {
    try {
        [void]$lock.WaitOne()
    }
    catch [System.Threading.AbandonedMutexException] {
        # A build that was stopped left the lock behind; this one holds it now.
    }

    if (Test-UpToDate) {
        Write-Host "$Output is up to date"
    }
    else {
        Invoke-Build
    }
}
finally {
    $lock.ReleaseMutex()
    $lock.Dispose()
}
