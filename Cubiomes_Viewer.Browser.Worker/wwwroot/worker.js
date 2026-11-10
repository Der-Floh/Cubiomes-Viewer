import { dotnet } from './_framework/dotnet.js';

const bootStarted = performance.now();
const { getAssemblyExports, getConfig } = await dotnet.create();
const assemblyExports = await getAssemblyExports(getConfig().mainAssemblyName);
const host = assemblyExports.Cubiomes_Viewer.Browser.Worker.WorkerExports;

const commands = {
    selfTest: () => host.RunSelfTest(),
    biomeTile: ({ seed, scale, size }) => host.GenerateBiomeTile(seed, scale, size),
};

self.onmessage = event => {
    const { id, command, args } = event.data;
    const started = performance.now();
    try {
        const result = commands[command](args ?? {});
        self.postMessage({ id, result, elapsedMs: performance.now() - started });
    } catch (error) {
        self.postMessage({ id, error: String(error), elapsedMs: performance.now() - started });
    }
};

self.postMessage({ ready: true, bootMs: performance.now() - bootStarted });
