const databaseName = 'cubiomes-viewer';
const storeName = 'documents';
let database;

// A connection that failed to open or was closed later is opened again by the next call.
function openDatabase() {
    database ??= new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(storeName, { keyPath: 'key' });
        request.onsuccess = () => {
            const opened = request.result;
            opened.onclose = () => database = undefined;
            opened.onversionchange = () => {
                opened.close();
                database = undefined;
            };
            resolve(opened);
        };
        request.onerror = () => reject(request.error);
    }).catch(error => {
        database = undefined;
        throw error;
    });
    return database;
}

async function withStore(mode, operation) {
    const openedDatabase = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = openedDatabase.transaction(storeName, mode);
        const request = operation(transaction.objectStore(storeName));
        const fail = () => reject(transaction.error ?? new Error('The storage transaction was aborted.'));

        transaction.oncomplete = () => resolve(request.result);
        transaction.onerror = fail;
        transaction.onabort = fail;
    });
}

export async function read(key) {
    return (await withStore('readonly', store => store.get(key))) ?? null;
}

export async function write(key, content) {
    await withStore('readwrite', store => store.put({ key, content }));
}

export async function remove(key) {
    await withStore('readwrite', store => store.delete(key));
}
