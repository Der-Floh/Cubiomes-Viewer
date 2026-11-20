#!/usr/bin/env bash
# Publishes a project for linux-x64 ahead of time inside the manylinux_2_28 container, with the runner's .NET SDK and NuGet
# cache, so the native executable runs on glibc 2.28 like the native library; then checks that it needs nothing newer.
# Usage: native/publish-manylinux.sh <project> <version> <publish-dir> [publish-profile]
set -euo pipefail

image="quay.io/pypa/manylinux_2_28_x86_64"
max_glibc="2.28"

project="$1"
version="$2"
publish_dir="$3"
profile="${4:-release}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_root="${DOTNET_ROOT:-$(dirname "$(readlink -f "$(command -v dotnet)")")}"
nuget_cache="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
mkdir -p "$publish_dir" "$nuget_cache"
publish_dir="$(cd "$publish_dir" && pwd)"

docker run --rm \
    --volume "$repo_root:/src" --volume "$publish_dir:/out" --volume "$dotnet_root:/opt/dotnet:ro" --volume "$nuget_cache:/nuget" \
    --workdir /src \
    --env NUGET_PACKAGES=/nuget --env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 --env DOTNET_NOLOGO=1 --env DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    --env HOST_OWNER="$(id -u):$(id -g)" \
    "$image" bash -euo pipefail -c '
        dnf install --assumeyes --quiet clang zlib-devel
        status=0
        /opt/dotnet/dotnet publish "$0" -r linux-x64 -p:PublishProfile="$1" -p:Version="$2" -p:PublishDir=/out/ -clp:ForceConsoleColor || status=$?
        chown -R "$HOST_OWNER" /src /out /nuget
        exit $status
    ' "$project" "$profile" "$version"

too_new=0
while IFS= read -r -d '' file; do
    head -c 4 "$file" | grep -q $'\x7fELF' || continue
    required="$(objdump -T "$file" | grep -o 'GLIBC_[0-9][0-9.]*' | sed 's/^GLIBC_//' | sort -V | tail -n 1)"
    if [[ -n "$required" && "$(printf '%s\n' "$required" "$max_glibc" | sort -V | tail -n 1)" != "$max_glibc" ]]; then
        echo "$file needs glibc $required, newer than the $max_glibc the releases run on." >&2
        too_new=1
    else
        echo "$file runs on glibc ${required:-any}."
    fi
done < <(find "$publish_dir" -maxdepth 1 -type f -print0)

exit $too_new
