#!/usr/bin/env bash
set -euo pipefail

rid="${1:?Usage: build.sh <rid> (linux-x64, linux-arm64, osx-x64, osx-arm64)}"

native_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source_file="$native_root/shim/cvnative.c"
output_dir="$native_root/bin/$rid"
flags=(-std=gnu17 -O3 -fwrapv -ffp-contract=off -fvisibility=hidden -ffunction-sections -fdata-sections)
macos_minimum="12.0"

build_macos() {
    local arch="$1"
    output="$output_dir/libcvnative.dylib"
    clang "${flags[@]}" -arch "$arch" -mmacosx-version-min="$macos_minimum" -dynamiclib -Wl,-dead_strip \
        -install_name @rpath/libcvnative.dylib -o "$output" "$source_file"

    local minimum
    minimum="$(otool -l "$output" | awk '$1 == "minos" { print $2; exit }')"
    if [[ "$minimum" != "$macos_minimum" ]]; then
        echo "$output needs macOS $minimum instead of $macos_minimum." >&2
        exit 1
    fi
}

if [[ ! -f "$native_root/cubiomes/generator.c" ]]; then
    echo "cubiomes sources are missing. Run 'git submodule update --init' in the repository root." >&2
    exit 1
fi

mkdir -p "$output_dir"

case "$rid" in
    linux-x64 | linux-arm64)
        output="$output_dir/libcvnative.so"
        "${CC:-cc}" "${flags[@]}" -fPIC -shared -Wl,--gc-sections -o "$output" "$source_file" -lm
        ;;
    osx-x64)
        build_macos x86_64
        ;;
    osx-arm64)
        build_macos arm64
        ;;
    *)
        echo "build.sh does not build '$rid'. Supported: linux-x64, linux-arm64, osx-x64, osx-arm64. Use build.ps1 for Windows targets." >&2
        exit 1
        ;;
esac

echo "Built $output"
