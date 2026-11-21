export function replaceAddress(address) {
    if (address !== location.href)
        history.replaceState(history.state, "", address);
}

export function pushAddress(address, step) {
    history.pushState({ cvStep: step }, "", address);
}

export function historyStep() {
    return history.state?.cvStep ?? 0;
}

export function goBack() {
    history.back();
}

export function goForward() {
    history.forward();
}

export function watchHistory(onMoved) {
    addEventListener("popstate", event => onMoved(location.href, event.state?.cvStep ?? 0));
}
