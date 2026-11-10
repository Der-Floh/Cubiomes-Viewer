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

### Browser workers

Heavy work in the browser runs in Web Workers, each hosting its own small .NET runtime (`Cubiomes_Viewer.Browser.Worker`) with the engine and the natively linked generator. In development the Browser head references the worker project, which serves it under `/worker/`. On publish the worker is published separately and copied to `wwwroot/worker/`, because the WebAssembly SDK would otherwise merge the worker's assemblies into the main app's boot manifest. Always publish with `dotnet publish Cubiomes_Viewer.Browser -c Release`.

Opening the app with `?workertest` boots two workers and prints their start-up and generation times to the browser console. Reference numbers from a Release publish: a worker boots in 50–80 ms, the published worker bundle is 3.2 MB (1 MB with Brotli), and a 128×128 biome tile for 1.21 takes 35–60 ms.

## License

[MIT](LICENSE.txt). Third-party licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
