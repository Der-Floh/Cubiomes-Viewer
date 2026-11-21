const workers = new Map();
const pendingRequests = new Map();
let nextRequestId = 1;

export function createWorker(workerId, scriptUrl) {
    return new Promise((resolve, reject) => {
        const worker = new Worker(new URL(scriptUrl, document.baseURI), { type: 'module' });
        workers.set(workerId, worker);

        const fail = message => {
            terminate(workerId);
            reject(new Error(message));
        };

        worker.onerror = event => fail(event.message || 'The worker script failed.');
        worker.onmessage = event => {
            const message = event.data;
            if (message.ready === true) {
                resolve(message.bootMs);
            } else if (message.ready === false) {
                fail(message.error);
            } else {
                settle(message);
            }
        };
    });
}

export function execute(workerId, request) {
    return new Promise((resolve, reject) => {
        const worker = workers.get(workerId);
        if (!worker) {
            reject(new Error(`Worker ${workerId} is not running.`));
            return;
        }

        const id = nextRequestId++;
        pendingRequests.set(id, { workerId, resolve, reject });
        worker.postMessage({ id, request });
    });
}

export function terminate(workerId) {
    workers.get(workerId)?.terminate();
    workers.delete(workerId);

    for (const [id, request] of pendingRequests) {
        if (request.workerId === workerId) {
            pendingRequests.delete(id);
            request.reject(new Error('The worker stopped before it answered.'));
        }
    }
}

function settle(message) {
    const request = pendingRequests.get(message.id);
    if (!request) {
        return;
    }

    pendingRequests.delete(message.id);
    if (message.error) {
        request.reject(new Error(message.error));
    } else {
        request.resolve(message);
    }
}
