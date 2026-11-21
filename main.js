import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();
const exports = await dotnetRuntime.getAssemblyExports(config.mainAssemblyName);

document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "hidden")
        exports.Program.FlushStoredStateAsync().catch(error => console.error(error));
});

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
