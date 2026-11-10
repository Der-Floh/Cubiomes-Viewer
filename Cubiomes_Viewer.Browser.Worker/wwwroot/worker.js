import { dotnet } from './_framework/dotnet.js';

const bootStarted = performance.now();

try {
    const { getAssemblyExports, getConfig } = await dotnet.create();
    const assemblyExports = await getAssemblyExports(getConfig().mainAssemblyName);
    const host = assemblyExports.Cubiomes_Viewer.Browser.Worker.WorkerExports;

    self.onmessage = event => {
        const { id, request } = event.data;
        try {
            self.postMessage({ id, response: host.Execute(request) });
        } catch (error) {
            self.postMessage({ id, error: String(error) });
        }
    };

    self.postMessage({ ready: true, bootMs: performance.now() - bootStarted });
} catch (error) {
    self.postMessage({ ready: false, error: String(error) });
}
