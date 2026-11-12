# Cubiomes Viewer

A cross-platform Minecraft Java Edition seed finder and biome map viewer for Desktop, Browser, Android and iOS, built with Avalonia UI on top of the [cubiomes](https://github.com/Cubitect/cubiomes) world-generation library.

The project is in early development and has no release yet.

## Building

Requirements:

- .NET 10 SDK with the `wasm-tools`, `android` and `ios` workloads for the respective heads.
- A C compiler for the native `cvnative` library: clang, or the Visual Studio C++ build tools on Windows.

Clone with submodules, then build and test:

```bash
git clone --recurse-submodules <repository-url>
```

```bash
dotnet test Cubiomes_Viewer.Engine.Tests
```

The native library is built automatically when a head or test project is built and its output is missing or older than the sources. It can also be built directly with `native/build.ps1 -Rid win-x64`.

Run the Desktop or Browser head:

```bash
dotnet run --project Cubiomes_Viewer.Desktop
```

```bash
dotnet run --project Cubiomes_Viewer.Browser
```

### Browser workers and storage

Heavy work in the browser runs in a pool of Web Workers (one per logical processor minus one, at most six), each hosting its own small .NET runtime (`Cubiomes_Viewer.Browser.Worker`) with the engine and the natively linked generator. Jobs and results cross as serialized messages. A worker that fails is replaced and its job is given to another worker once; when the browser cannot start any worker, the same jobs run on the main thread instead. Settings and the search session are kept in IndexedDB.

In development the Browser head references the worker project, which serves it under `/worker/`. On publish the worker is published separately and copied to `wwwroot/worker/`, because the WebAssembly SDK would otherwise merge the worker's assemblies into the main app's boot manifest. Always publish with `dotnet publish Cubiomes_Viewer.Browser -c Release`.

### Seed search

The Search panel finds seeds that meet a list of conditions: structures, biomes, the world spawn, strongholds and slime chunks in an area, each counted or excluded and optionally looking around where another condition was found, plus "any of" to accept one of several. Seeds are scanned in runs of consecutive seeds on the compute workers, in the version and biome size the map shows. A quick pass decides what it can without generating biomes (where structures attempt to generate, slime chunks) and leaves only the remaining seeds to the full pass; the engine tests check on random seeds that this never changes which seeds are found. The setup and the search are stored shortly after each change and every few seconds while a search runs, so a stopped or interrupted search continues after a restart, and both can be saved to and opened from a file.

### Diagnostics

Settings → Diagnostics → Run self-test checks the native generator against reference values and measures tile throughput on the compute workers of the current platform. Reference numbers from Release builds, for 64 biome tiles of 128×128 samples at 1:4 in 1.21: 180–200 tiles/s on 16 Desktop worker threads, 75–105 tiles/s on 6 Web Workers depending on what else the machine is doing, and about 20 tiles/s when the browser falls back to the main thread. The same button checks structures with their variants and pieces, strongholds, End gateways, the spawn point, slime chunks and the other map layers (climate parameters, rivers, ocean temperatures and surface heights) against values recorded from the Windows build, which is how other platforms prove they generate identical worlds. The published worker bundle is 6.8 MB (2.1 MB with Brotli).

## License

[MIT](LICENSE.txt). Third-party licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
