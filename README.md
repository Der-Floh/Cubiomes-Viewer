# Cubiomes Viewer

A cross-platform Minecraft Java Edition seed finder and biome map viewer, built on top of the [cubiomes](https://github.com/Cubitect/cubiomes) world-generation library.

![Cubiomes Viewer searching for seeds with mushroom fields near spawn, next to the shaded biome map of seed 262](docs/screenshot.png)

[Open the web version](https://der-floh.github.io/Cubiomes-Viewer) · [Download for Windows, Linux or macOS](https://github.com/Der-Floh/Cubiomes-Viewer/releases)

## Features

- **Biome map** of any seed from Beta 1.7 to 1.21.3, in the Overworld, Nether and End, with structures, strongholds, the spawn point and slime chunks marked.
- **More layers**: climate parameters, surface heights, rivers, ocean temperatures and where a structure type can generate, plus optional relief shading of the biome map.
- **Seed search** for seeds that meet a list of conditions, in the app or from the command line.
- **Analysis** of the biomes, structures and locations around a point, for one seed or up to 1,000 found seeds.
- **Export** of any layer as PNG images of any size, whole or cut into tiles.
- **Map tools**: pins, distance measurements, links to a spot, find nearest structure or biome, and the matching portal spot in the other dimension.
- **Desktop and web**: Windows, Linux, macOS and the browser, with in-app updates on the desktop.

### Map

- Zoom from single blocks out to whole regions, and drag with the left or right mouse button.
- The world bar sets the seed (or a random one), version, large biomes, dimension and the Y level biomes are read at; Shift + mouse wheel steps the Y level.
- The status bar shows the biome or value, block and chunk under the pointer.
- Layers: biomes, structure potential, climate parameters (temperature, humidity, continentalness, erosion, depth, weirdness), rivers, ocean temperatures and surface height, as far as the version has them.
- Surface heights can be tinted, shaded, drawn as contour lines or in grey. Settings → Map can also shade the biome map by terrain height.
- Markers for the structure types you choose, down to their variants, plus the world spawn, strongholds, slime chunks and End gateways.
- Biomes in the default colours or in colour maps of your own, edited live on the map and shared as JSON files.
- On the desktop, the map can move into a window of its own, for a second screen.

### Right-click menu

- Copy the block's coordinates, `/tp` command, chunk, region file or the seed.
- Put a pin on the block, or measure the distance to another one. Pins and measurements are kept per seed and dimension and can be named.
- **Copy link to this spot**: opens the web version on the same world and view, for example `https://der-floh.github.io/Cubiomes-Viewer/?seed=262&version=1.21.3&dimension=nether&x=100&z=-50&scale=4`. A link pasted into the seed box opens the same way, on the desktop too.
- **Find nearest structure or biome**: like the game's `/locate`, as far out as the game looks. The map then flies to the result.
- **Open in the Nether or Overworld**: the matching spot in the other dimension, for planning portals.

### Seed search

- **Conditions**, each required or excluded, and each able to look around where another one was found:
  - structures, optionally of one kind (end cities with an end ship, abandoned villages, igloos with a basement, giant ruined portals), and four huts close enough for one farm
  - biomes: one of them, each of them, a share of the area, or a patch of a size
  - climate parameters of the 1.18+ Overworld, and the temperature categories of 1.7 to 1.17
  - the world spawn, strongholds, slime chunks, and "any of" several conditions
- **Seeds** from a range, a list, structure seeds (each with its 65,536 sister seeds) or the quad-hut bases.
- **Fast**: a quick pass rules seeds out without generating biomes, about 68 million structure seeds a second on 15 desktop threads.
- **Kept**: a stopped or interrupted search continues after a restart, and can be saved to and opened from a file.
- **Examples** show what a search can look for, and any setup can be kept as a preset.

### Command line

`cubiomes-viewer-cli` runs a search saved with Save to file in the Search panel without the app, for example on a server:

```bash
cubiomes-viewer-cli --session search.json --out seeds.txt
```

- Seeds are printed as they are found, or written to the file given with `--out`; progress shows in the terminal.
- Progress is saved back into the session file, so Ctrl+C stops the search, the same command continues it, and the app opens it where the CLI left off. `--reset` starts over.
- `--selftest` runs the app's self-test.
- Exit codes: 0 finished, 1 failed, 2 invalid arguments, 130 stopped.

### Analysis

- For the seed on the map, or up to 1,000 found seeds, in a square or circle around a point:
  - **Biomes**: how many samples each biome has, and its sample nearest to the centre
  - **Structures**: nearest first with their details, and quads of swamp huts, desert pyramids, jungle temples or igloos
  - **Locations**: the spawn point, strongholds, and where the search conditions hold
- Every list can be saved as a CSV file, and choosing a line opens its seed at that place on the map.

### Export

- Saves the layer the map shows as one PNG per seed, at any scale and size; several seeds, or an image cut into tiles, become a ZIP archive.
- Biome PNGs keep the biome id as palette index, so tools can read the biomes back. Other layers, and a biome map shaded by terrain height, are written in colour.
- Images are written row by row, so even huge ones need little memory; the panel warns about images many programs cannot open.
- Strong compression: 3 to 24 % smaller than zlib's default level with a fixed PNG filter.

### Settings

- **Appearance**: theme and interface size.
- **Map**: smooth motion, relief shading of the biome map, tile cache, grid spacing, marker size, and how far out each structure type is marked.
- **World generation**: the experimental versions (Beta 1.7, Beta 1.8, 1.0 and the 1.21 Winter Drop), the terrain estimate that leaves out some structures on low land from 1.18 on, and replaced structure salts, as servers and data packs use them.
- **Start**: open the last world and search, and keep the desktop window's size and position.
- **Export**: the separator of CSV files.
- **Updates** (desktop): the installed version, a check on request, and the check at start.
- **Diagnostics**: Run self-test checks the generator against reference values and measures tile throughput, about 180–200 tiles/s on 16 desktop threads and 75–105 on 6 Web Workers.

## Installing

Each release on [GitHub](https://github.com/Der-Floh/Cubiomes-Viewer/releases) carries:

- **Windows** (x64 and ARM64): a setup and a portable zip. The setup is also on winget as `Der_Floh.CubiomesViewer`.
- **Linux** (x64): an AppImage.
- **macOS** (Apple Silicon and Intel): an installer package and a zipped app. Neither is signed, so macOS blocks the first start until it is allowed in System Settings → Privacy & Security → Open Anyway.
- **Command line**: the search for each of these systems, as a zip on Windows and a `.tar.gz` elsewhere.

The [web version](https://der-floh.github.io/Cubiomes-Viewer) needs no installation.

Installed desktop copies keep themselves up to date: at start the app looks for a newer release, downloads it in the background and installs it when the app closes. Settings → Updates restarts into a downloaded update right away or turns the check off.

## Building

Requirements:

- .NET 10 SDK, with the `wasm-tools` workload for the Browser head.
- A C compiler for the native `cvnative` library: clang, or the Visual Studio C++ build tools on Windows.

Clone with submodules, then build and test:

```bash
git clone --recurse-submodules https://github.com/Der-Floh/Cubiomes-Viewer
```

```bash
dotnet test Cubiomes_Viewer.Engine.Tests
```

The native library is built automatically when a head or test project is built and its output is missing or older than the sources. It can also be built directly with `native/build.ps1 -Rid win-x64`.

Run the Desktop or Browser head, or the command line from source:

```bash
dotnet run --project Cubiomes_Viewer.Desktop
```

```bash
dotnet run --project Cubiomes_Viewer.Browser
```

```bash
dotnet run --project Cubiomes_Viewer.Cli -- --session search.json --out seeds.txt
```

### Browser workers and storage

- Heavy work runs in a pool of Web Workers (one per logical processor minus one, at most six), each hosting a small .NET runtime (`Cubiomes_Viewer.Browser.Worker`) with the engine and the natively linked generator. Jobs and results cross as serialized messages.
- A worker that fails is replaced and its job given to another worker once; when no worker can start, the jobs run on the main thread instead.
- Settings and the search session are kept in IndexedDB.
- In development the Browser head serves the worker project under `/worker/`. On publish the worker is published separately into `wwwroot/worker/`, because the WebAssembly SDK would otherwise merge its assemblies into the main app's boot manifest. Always publish with `dotnet publish Cubiomes_Viewer.Browser -c Release`.

### Tests

- The engine tests compare map tiles, search and find-nearest results, analysis counts and exported pixels with the generator, and pin fourteen exports of fixed worlds by their hashes.
- The CLI tests run example searches through the CLI and through the app's search and compare the seeds, also across a stop and a second run.
- The self-test checks the generator, structures, strongholds, End gateways, the spawn point, slime chunks and the other layers against values recorded from the Windows build, which is how other platforms prove they generate identical worlds.

## License

[MIT](LICENSE.txt). Third-party licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
