# Cubiomes Viewer

A cross-platform Minecraft Java Edition seed finder and biome map viewer for Desktop, Browser, Android and iOS, built with Avalonia UI on top of the [cubiomes](https://github.com/Cubitect/cubiomes) world-generation library.

The project is in early development and has no release yet.

## Installing

Each release on [GitHub](https://github.com/Der-Floh/Cubiomes-Viewer/releases) carries:

- Windows (x64 and ARM64): a setup and a portable zip. The setup is also on winget as `Der_Floh.CubiomesViewer`.
- Linux (x64): an AppImage.
- macOS (Apple Silicon and Intel): an installer package and a zipped app. Neither is signed, so macOS blocks the first start until it is allowed in System Settings → Privacy & Security → Open Anyway.
- The command-line search for each of these systems, as a zip on Windows and a `.tar.gz` elsewhere.

The [web version](https://der-floh.github.io/Cubiomes-Viewer) needs no installation.

Installed desktop copies keep themselves up to date. When the app starts it looks for a newer release on GitHub, downloads it in the background and installs it when the app closes; Settings → Updates shows the version, checks on request, restarts into a downloaded update right away and turns the check at start off.

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

### Releases

Publishing a release on GitHub runs `.github/workflows/publish.yml`. The tag sets the version, such as `v1.2.0`; a prerelease tag such as `v1.2.0-beta.1` makes a prerelease, which installed copies do not update to. The workflow publishes the desktop app self-contained for win-x64, win-arm64, linux-x64, osx-arm64 and osx-x64 with the `release` publish profile and packs each with Velopack as `CubiomesViewer`, with one update channel per runtime and no deltas. It installs and uninstalls each Windows setup, publishes the CLI for the same runtimes, uploads everything to the release together with the update feeds the installed apps read (`releases.<runtime>.json` and the `.nupkg` files), and then updates the winget package, whose first version has to be submitted by hand. `pages.yml` publishes the web version to GitHub Pages with each release. CI runs the engine and CLI tests on Windows, Linux and macOS, so a platform that generates different worlds fails the golden tests, and it runs the UI tests on Windows, publishes the web version and installs a Windows setup.

Release packages can be tried before they are published: pack two versions into one folder with `vpk pack` and the options `publish.yml` uses, install the older one with its setup and start it with the environment variable `CUBIOMES_VIEWER_UPDATE_FEED` set to that folder. It then updates from the folder instead of from GitHub.

### Browser workers and storage

Heavy work in the browser runs in a pool of Web Workers (one per logical processor minus one, at most six), each hosting its own small .NET runtime (`Cubiomes_Viewer.Browser.Worker`) with the engine and the natively linked generator. Jobs and results cross as serialized messages. A worker that fails is replaced and its job is given to another worker once; when the browser cannot start any worker, the same jobs run on the main thread instead. Settings and the search session are kept in IndexedDB.

In development the Browser head references the worker project, which serves it under `/worker/`. On publish the worker is published separately and copied to `wwwroot/worker/`, because the WebAssembly SDK would otherwise merge the worker's assemblies into the main app's boot manifest. Always publish with `dotnet publish Cubiomes_Viewer.Browser -c Release`.

### Seed search

The Search panel finds seeds that meet a list of conditions, each counted or excluded and optionally looking around where another condition was found:

- Structures, optionally of one kind (end cities with an end ship, abandoned villages, igloos with a basement, giant ruined portals), and four huts close enough for one farm.
- Biomes: one of them, each of them, or a share of the area judged from random points that follow from the seed, which suits large 1.18+ areas a grid would take too long for.
- Patches of biomes of a size, each found at its middle so that other conditions can look around it.
- Climate parameters of the 1.18+ Overworld that lie in a range everywhere, somewhere or on average.
- Temperature categories, which 1.7 to 1.17 settle at 1:1024 before any biome: a cheap first look for jungles, badlands or giant tree taigas.
- The world spawn, strongholds and slime chunks in an area, and "any of" to accept one of several.

Seeds are scanned in batches on the compute workers, in the version and biome size the map shows. A quick pass decides what it can without generating biomes (where structures attempt to generate, slime chunks) and leaves only the remaining seeds to the full pass; the engine tests check on random seeds that this never changes which seeds are found. The setup and the search are stored shortly after each change and every few seconds while a search runs, so a stopped or interrupted search continues after a restart, and both can be saved to and opened from a file.

The seeds come from a range, from a list, from structure seeds, or from the quad-hut bases. A structure seed is the lower 48 bits of a seed, which alone decide where structures attempt to generate and where slime chunks are, so the quick pass looks at each structure seed once and only one it cannot rule out goes on with its 65,536 sister seeds; with 15 worker threads on Desktop that rules out about 68 million structure seeds a second. Workers may hand back part of a batch once they run out of time or have the matches the search still needs, and the search gives the rest to another batch. The quad-hut bases are the lower 48 bits of every seed that places four swamp huts, desert temples, jungle temples or igloos near the corner where four regions meet, each tried with all 65,536 upper halves, since only the biomes depend on those. The bases are found on the compute workers the first time a search needs them and kept in app data; the engine tests check them against the known totals: 74,474 ideal and 33,485 further classic constellations, 185,757 more with room for a 43-block drop and 462,029 more with all four huts in range of one point. Examples show what a search can look for, and any setup can be kept as a preset of your own.

### Command line

`Cubiomes_Viewer.Cli` builds `cubiomes-viewer-cli`, which runs a search saved with Save to file in the Search panel without the app, for example on a server:

```bash
dotnet run --project Cubiomes_Viewer.Cli -- --session search.json --out seeds.txt
```

It treats the session as the app's Search panel does after opening it: a stopped search whose setup is unchanged continues, any other one starts over, and `--reset` always starts over. Seeds go to the standard output, or with `--out` to a file, one per line as they are found; messages and progress go to the standard error, as a status line that updates in place in a terminal. The progress and the seeds are saved back into the session file every 10 seconds and when the search stops, so Ctrl+C stops it, running the same command again continues it, and opening the file in the app shows the search where the CLI left it. A session whose search finished lists its seeds without searching again. `--selftest` runs the checks of the app's self-test. The exit code is 0 when the search finished, 1 when it failed, 2 for invalid arguments and 130 when it was stopped before finishing. The CLI tests run example searches saved as session files through the CLI and through the app's search and compare the seeds, also across a stop and a second run.

### Analysis

The Analysis panel looks more closely at the seed on the map, or at up to 1,000 found seeds in the version they were found in, in a square or circle around a point:

- Biomes: how many samples of the area each biome has and where its sample nearest to the centre is, at a chosen spacing and height. Large areas are split into bands of rows on the compute workers; the engine tests check the counts against counting every sample of the area directly.
- Structures: the structures of the chosen types, nearest first and with their details, and the quads of swamp huts, desert pyramids, jungle temples or igloos with how many of the four generate.
- Locations: the spawn point, the strongholds in the area, and where the conditions of the Search panel hold for each seed.

Every list can be saved as a CSV file, and choosing a line opens its seed at that place on the map.

### Export

The Export panel saves the layer the map shows, as the Layers panel sets it up, with one pixel per sample: biomes and every other layer, at any scale the layer has, over an area that starts as the visible one. Biome layers are written as PNG images whose palette index is the biome id, with the id of no biome transparent, so tools can read the biomes back from the file; other layers are written in colour. An export of the found seeds, or one cut into tiles, becomes a ZIP archive named after the seeds and tiles. Rows are generated on the compute workers and written as they are ready, so even 32,768-pixel images need little memory, and stopping removes the half-written file. The engine tests compare exported pixels with the generator and pin the content of ten exports of fixed worlds by their hashes.

### Colours, world settings and preferences

The Layers panel paints biomes in their default colours or in a colour map of your own. Its editor lists the biomes of the version on the map with a colour picker each, shows every change on the map at once, and keeps it only when saved; maps can be exported to and imported from JSON files that write colours as `#rrggbb`. Exports use the colours the map shows.

Settings → World generation offers the experimental versions (Beta 1.7, Beta 1.8, 1.0 and the 1.21 Winter Drop, which the generator supports less completely), switches off the terrain estimate that leaves out desert pyramids, jungle temples and woodland mansions on low land from 1.18 on, and replaces the salt of any structure type, as servers and data packs can. Replaced salts reach the map, searches and analysis alike; the engine tests check that a salt raised by some amount moves structures exactly as raising the seed by that amount does. Settings also set the grid spacing and how it widens when zoomed out, the marker size, how far out each structure type is marked, whether the last world and search open at start, whether the Desktop window keeps its size and position, and the separator of CSV files. On the desktop, the button below the rail moves the map into a window of its own, for a second screen.

### Diagnostics

Settings → Diagnostics → Run self-test checks the native generator against reference values and measures tile throughput on the compute workers of the current platform. Reference numbers from Release builds, for 64 biome tiles of 128×128 samples at 1:4 in 1.21: 180–200 tiles/s on 16 Desktop worker threads, 75–105 tiles/s on 6 Web Workers depending on what else the machine is doing, and about 20 tiles/s when the browser falls back to the main thread. The same button checks structures with their variants and pieces, strongholds, End gateways, the spawn point, slime chunks and the other map layers (climate parameters, rivers, ocean temperatures and surface heights) against values recorded from the Windows build, which is how other platforms prove they generate identical worlds. The published worker bundle is 6.8 MB (2.1 MB with Brotli).

## License

[MIT](LICENSE.txt). Third-party licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
