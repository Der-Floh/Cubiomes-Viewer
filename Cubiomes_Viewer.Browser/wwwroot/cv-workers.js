const workers = new Map();
const pending = new Map();
let nextRequestId = 1;

export function createWorker(workerId, scriptUrl) {
    return new Promise((resolve, reject) => {
        const worker = new Worker(new URL(scriptUrl, document.baseURI), { type: 'module' });
        workers.set(workerId, worker);

        worker.onerror = event => reject(new Error(event.message || 'The worker failed to start.'));
        worker.onmessage = event => {
            const message = event.data;
            if (message.ready) {
                resolve(message.bootMs);
                return;
            }

            const request = pending.get(message.id);
            pending.delete(message.id);
            if (message.error) {
                request.reject(new Error(message.error));
            } else {
                request.resolve(message);
            }
        };
    });
}

function send(workerId, command, args) {
    const id = nextRequestId++;
    return new Promise((resolve, reject) => {
        pending.set(id, { resolve, reject });
        workers.get(workerId).postMessage({ id, command, args });
    });
}

export async function runSelfTest(workerId) {
    const message = await send(workerId, 'selfTest');
    return `${message.result} (worker time ${message.elapsedMs.toFixed(0)} ms)`;
}

export async function measureBiomeTile(workerId, seed, scale, size) {
    const message = await send(workerId, 'biomeTile', { seed, scale, size });
    return `${message.result.length} samples in ${message.elapsedMs.toFixed(1)} ms`;
}

export function terminate(workerId) {
    workers.get(workerId)?.terminate();
    workers.delete(workerId);
}
