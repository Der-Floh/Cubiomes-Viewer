#!/usr/bin/env bash
set -euo pipefail

rid="${1:?Usage: build.sh <rid> (linux-x64, linux-arm64, osx-x64, osx-arm64)}"

native_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source_file="$native_root/shim/cvnative.c"
output_dir="$native_root/bin/$rid"
lock_dir="$native_root/obj/$rid.lock"
flags=(-std=gnu17 -O3 -fwrapv -ffp-contract=off -fvisibility=hidden -ffunction-sections -fdata-sections)
macos_minimum="12.0"
lock_attempts=600

case "$rid" in
    linux-x64 | linux-arm64) output="$output_dir/libcvnative.so" ;;
    osx-x64 | osx-arm64) output="$output_dir/libcvnative.dylib" ;;
    *)
        echo "build.sh does not build '$rid'. Supported: linux-x64, linux-arm64, osx-x64, osx-arm64. Use build.ps1 for Windows targets." >&2
        exit 1
        ;;
esac

build_macos() {
    local arch="$1"
    clang "${flags[@]}" -arch "$arch" -mmacosx-version-min="$macos_minimum" -dynamiclib -Wl,-dead_strip \
        -install_name @rpath/libcvnative.dylib -o "$output" "$source_file"

    local minimum
    minimum="$(otool -l "$output" | awk '$1 == "minos" { print $2; exit }')"
    if [[ "$minimum" != "$macos_minimum" ]]; then
        echo "$output needs macOS $minimum instead of $macos_minimum." >&2
        exit 1
    fi
}

# Projects built side by side run this script at once; the lock lets one build and the others find it up to date.
# flock holds until the script ends; macOS has no flock, and mkdir is atomic on its file systems.
acquire_lock() {
    if command -v flock >/dev/null; then
        exec 9>"$lock_dir"
        flock 9
        return
    fi

    local attempt=0
    until mkdir "$lock_dir.d" 2>/dev/null; do
        if ((attempt++ >= lock_attempts)); then
            echo "Taking over $lock_dir.d, which a build has held for two minutes." >&2
            rm -rf "$lock_dir.d"
            attempt=0
        fi
        sleep 0.2
    done
    trap 'rmdir "$lock_dir.d" 2>/dev/null' EXIT
}

is_up_to_date() {
    [[ -f "$output" ]] && [[ -z "$(find "$native_root/shim" "$native_root/cubiomes" "${BASH_SOURCE[0]}" -newer "$output" \
        \( -name '*.c' -o -name '*.h' -o -name '*.inc' -o -name 'build.sh' \) -print -quit)" ]]
}

if [[ ! -f "$native_root/cubiomes/generator.c" ]]; then
    echo "cubiomes sources are missing. Run 'git submodule update --init' in the repository root." >&2
    exit 1
fi

mkdir -p "$output_dir" "$native_root/obj"
acquire_lock
if is_up_to_date; then
    echo "$output is up to date"
    exit 0
fi

case "$rid" in
    linux-x64 | linux-arm64)
        "${CC:-cc}" "${flags[@]}" -fPIC -shared -Wl,--gc-sections -o "$output" "$source_file" -lm
        ;;
    osx-x64)
        build_macos x86_64
        ;;
    osx-arm64)
        build_macos arm64
        ;;
esac

echo "Built $output"
