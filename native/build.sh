#!/usr/bin/env bash
set -euo pipefail

rid="${1:?Usage: build.sh <rid> (linux-x64, linux-arm64, osx-x64, osx-arm64)}"

native_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source_file="$native_root/shim/cvnative.c"
output_dir="$native_root/bin/$rid"
flags=(-O3 -fwrapv -ffp-contract=off -fvisibility=hidden)

if [[ ! -f "$native_root/cubiomes/generator.c" ]]; then
    echo "cubiomes sources are missing. Run 'git submodule update --init' in the repository root." >&2
    exit 1
fi

mkdir -p "$output_dir"

case "$rid" in
    linux-x64 | linux-arm64)
        output="$output_dir/libcvnative.so"
        "${CC:-cc}" "${flags[@]}" -fPIC -shared -o "$output" "$source_file" -lm
        ;;
    osx-x64)
        output="$output_dir/libcvnative.dylib"
        clang "${flags[@]}" -arch x86_64 -dynamiclib -install_name @rpath/libcvnative.dylib -o "$output" "$source_file"
        ;;
    osx-arm64)
        output="$output_dir/libcvnative.dylib"
        clang "${flags[@]}" -arch arm64 -dynamiclib -install_name @rpath/libcvnative.dylib -o "$output" "$source_file"
        ;;
    *)
        echo "build.sh does not build '$rid'. Supported: linux-x64, linux-arm64, osx-x64, osx-arm64. Use build.ps1 for Windows targets." >&2
        exit 1
        ;;
esac

echo "Built $output"
