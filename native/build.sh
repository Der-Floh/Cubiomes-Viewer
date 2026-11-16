#!/usr/bin/env bash
set -euo pipefail

rid="${1:?Usage: build.sh <rid> (linux-x64, linux-arm64, osx-x64, osx-arm64, android-arm64, android-x64)}"

native_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
shim_source="$native_root/shim/cvnative.c"
output_dir="$native_root/bin/$rid"
object_dir="$native_root/obj/$rid"
shim_object="$object_dir/cvnative.o"
lock_dir="$native_root/obj/$rid.lock"
flags=(-std=gnu17 -O3 -fwrapv -ffp-contract=off -fvisibility=hidden -ffunction-sections -fdata-sections -DCJSON_HIDE_SYMBOLS)
macos_minimum="12.0"
android_api_level=24
lock_attempts=600

case "$rid" in
    linux-x64 | linux-arm64 | android-arm64 | android-x64) output="$output_dir/libcvnative.so" ;;
    osx-x64 | osx-arm64) output="$output_dir/libcvnative.dylib" ;;
    *)
        echo "build.sh does not build '$rid'. Supported: linux-x64, linux-arm64, osx-x64, osx-arm64, android-arm64, android-x64. Use build.ps1 for Windows targets." >&2
        exit 1
        ;;
esac

# noise.c is compiled through shim/cv_noise.c; tests.c and tests/ are cubiomes' own test programs.
collect_cubiomes_sources() {
    sources=("$native_root/shim/cv_noise.c")
    local file
    while IFS= read -r file; do
        sources+=("$file")
    done < <(find "$native_root/cubiomes" -name '*.c' ! -path "$native_root/cubiomes/tests/*" \
        ! -path "$native_root/cubiomes/tests.c" ! -path "$native_root/cubiomes/noise.c" | LC_ALL=C sort)
}

build_macos() {
    local arch="$1"
    local target=(-arch "$arch" -mmacosx-version-min="$macos_minimum")
    clang "${flags[@]}" "${target[@]}" -c -o "$shim_object" "$shim_source"
    clang "${flags[@]}" -w "${target[@]}" -dynamiclib -Wl,-dead_strip \
        -install_name @rpath/libcvnative.dylib -o "$output" "$shim_object" "${sources[@]}"

    local minimum
    minimum="$(otool -l "$output" | awk '$1 == "minos" { print $2; exit }')"
    if [[ "$minimum" != "$macos_minimum" ]]; then
        echo "$output needs macOS $minimum instead of $macos_minimum." >&2
        exit 1
    fi
}

is_android_ndk() {
    [[ -n "$1" && -f "$1/source.properties" ]]
}

# The NDK named by the environment, else the newest side-by-side NDK of an Android SDK, or its older single ndk-bundle.
find_android_ndk() {
    local candidate sdk newest
    for candidate in "${ANDROID_NDK_HOME:-}" "${ANDROID_NDK_ROOT:-}" "${ANDROID_NDK_LATEST_HOME:-}"; do
        if is_android_ndk "$candidate"; then
            echo "$candidate"
            return
        fi
    done

    for sdk in "${ANDROID_HOME:-}" "${ANDROID_SDK_ROOT:-}" "$HOME/Android/Sdk" "$HOME/Library/Android/sdk"; do
        [[ -n "$sdk" && -d "$sdk" ]] || continue
        if [[ -d "$sdk/ndk" ]]; then
            newest="$(find "$sdk/ndk" -mindepth 2 -maxdepth 2 -name source.properties -exec dirname {} \; | sort -V | tail -n 1)"
            if [[ -n "$newest" ]]; then
                echo "$newest"
                return
            fi
        fi
        if is_android_ndk "$sdk/ndk-bundle"; then
            echo "$sdk/ndk-bundle"
            return
        fi
    done

    echo "The Android NDK was not found. Install it with the Android SDK manager, or set ANDROID_NDK_HOME to it." >&2
    exit 1
}

build_android() {
    local triple="$1"
    local ndk host
    ndk="$(find_android_ndk)"
    case "$(uname -s)" in
        Linux) host=linux-x86_64 ;;
        Darwin) host=darwin-x86_64 ;;
        *)
            echo "build.sh cannot run the Android NDK on $(uname -s). Use build.ps1 on Windows." >&2
            exit 1
            ;;
    esac

    local clang="$ndk/toolchains/llvm/prebuilt/$host/bin/clang"
    local target="--target=$triple$android_api_level"
    "$clang" "$target" "${flags[@]}" -fPIC -c -o "$shim_object" "$shim_source"
    # Devices with 16 KB memory pages only load libraries whose segments are aligned to them.
    "$clang" "$target" "${flags[@]}" -w -fPIC -shared -Wl,--gc-sections -Wl,-z,max-page-size=16384 -s \
        -o "$output" "$shim_object" "${sources[@]}" -lm
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

mkdir -p "$output_dir" "$object_dir"
acquire_lock
if is_up_to_date; then
    echo "$output is up to date"
    exit 0
fi

# The shim is compiled with warnings on its own; cubiomes is third-party code, compiled and linked without them.
collect_cubiomes_sources
case "$rid" in
    linux-x64 | linux-arm64)
        "${CC:-cc}" "${flags[@]}" -fPIC -c -o "$shim_object" "$shim_source"
        "${CC:-cc}" "${flags[@]}" -w -fPIC -shared -Wl,--gc-sections -o "$output" "$shim_object" "${sources[@]}" -lm
        ;;
    osx-x64)
        build_macos x86_64
        ;;
    osx-arm64)
        build_macos arm64
        ;;
    android-arm64)
        build_android aarch64-linux-android
        ;;
    android-x64)
        build_android x86_64-linux-android
        ;;
esac

echo "Built $output"
