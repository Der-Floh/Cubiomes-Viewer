export function replaceAddress(address) {
    if (address !== location.href)
        history.replaceState(history.state, "", address);
}
