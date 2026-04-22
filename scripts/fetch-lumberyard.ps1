$ErrorActionPreference = "Stop"

# Fetches CryPhysics sources from aws/lumberyard into third_party/CryPhysicsNative/lumberyard-src/.
# Source: https://github.com/aws/lumberyard (Apache 2.0).

$LyCommit = if ($env:LY_COMMIT) { $env:LY_COMMIT } else { "master" }
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Resolve-Path "$ScriptDir\.."
$Dest = "$Root\lumberyard-src"
$Tmp = "$Root\.lumberyard-tmp"

if (Test-Path $Dest) {
    Write-Host "lumberyard-src\ already exists - delete it to re-fetch."
    exit 0
}

Write-Host "Cloning aws/lumberyard (sparse) at $LyCommit..."
if (Test-Path $Tmp) { Remove-Item -Recurse -Force $Tmp }
git clone --filter=blob:none --no-checkout --depth 1 --branch $LyCommit `
    https://github.com/aws/lumberyard.git $Tmp

Push-Location $Tmp
git sparse-checkout init --cone
git sparse-checkout set dev/Code/CryEngine/CryPhysics dev/Code/CryEngine/CryCommon dev/Code/Framework/AzCore
git checkout $LyCommit
Pop-Location

New-Item -ItemType Directory -Force -Path $Dest | Out-Null
Copy-Item -Recurse "$Tmp\dev\Code\CryEngine\CryPhysics\*" $Dest

New-Item -ItemType Directory -Force -Path "$Root\crycommon-min" | Out-Null
Copy-Item -Recurse "$Tmp\dev\Code\CryEngine\CryCommon\*" "$Root\crycommon-min"

# AzCore: preserve the stock Framework/AzCore + Platform layout. Lumberyard's
# `Platform/<os>/AzCore/...` headers use relative `#include <../Common/.../...>`
# paths, so we keep the same on-disk shape and point CMake at both roots.
New-Item -ItemType Directory -Force -Path "$Root\azcore-src" | Out-Null
Copy-Item -Recurse "$Tmp\dev\Code\Framework\AzCore\AzCore" "$Root\azcore-src\"
Copy-Item -Recurse "$Tmp\dev\Code\Framework\AzCore\Platform" "$Root\azcore-src\"

Remove-Item -Recurse -Force $Tmp

Write-Host "Done. Sources in $Dest, CryCommon headers in $Root\crycommon-min."
Write-Host "Next: cmake -B build -A x64"
Write-Host "Then: cmake --build build --config Release"
