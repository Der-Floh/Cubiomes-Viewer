#!/usr/bin/env bash
set -euo pipefail

image="quay.io/pypa/manylinux_2_28_x86_64"
max_glibc="2.28"

native_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
library="$native_root/bin/linux-x64/libcvnative.so"

docker run --rm --user "$(id -u):$(id -g)" --env CC=gcc \
    --volume "$(dirname "$native_root"):/src" --workdir /src "$image" bash native/build.sh linux-x64

required="$(objdump -T "$library" | grep -o 'GLIBC_[0-9][0-9.]*' | sed 's/^GLIBC_//' | sort -V | tail -n 1)"
if [[ "$(printf '%s\n' "$required" "$max_glibc" | sort -V | tail -n 1)" != "$max_glibc" ]]; then
    echo "$library needs glibc $required, newer than the $max_glibc the releases run on." >&2
    exit 1
fi

echo "$library runs on glibc $required and newer."
