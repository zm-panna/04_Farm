/* Reusable QR / product-barcode camera adapter. No business APIs, persistence,
 * recording, frame upload, automatic permissions, or application mutations. */
(function(root, factory) {
    const api = factory(root);
    if (typeof module === 'object' && module.exports) module.exports = api;
    else root.ParuScannerCamera = api;
})(typeof window !== 'undefined' ? window : globalThis, function(root) {
    'use strict';
    const VENDOR = '/lib/zxing-wasm-3.1.5/';
    const NATIVE_FORMATS = ['qr_code', 'code_128', 'ean_13', 'ean_8', 'upc_a', 'upc_e'];
    const FALLBACK_FORMATS = ['QRCode', 'Code128', 'EAN13', 'EAN8', 'UPCA', 'UPCE'];
    const WASM_SHA256 = 'aecc1876de036c62c8419f67a5e1a16b1698a325bcd190aa84810d516e263931';
    const SCRIPT_SRI = 'sha256-Io5tjMuEHFRDhu6v1fGClNPkqg56ogQZvFGgAJudPBc=';
    let vendorScript;
    const stopTracks = stream => { if (stream) for (const track of stream.getTracks()) track.stop(); };
    function cameraError(error) {
        if (['NotAllowedError', 'PermissionDeniedError', 'SecurityError'].includes(error?.name)) return 'denied';
        if (['NotFoundError', 'DevicesNotFoundError', 'NotReadableError', 'TrackStartError', 'OverconstrainedError'].includes(error?.name)) return 'unavailable';
        return 'decoder';
    }
    function loadVendor(env) {
        if (env.ZXingWASM) return Promise.resolve(env.ZXingWASM);
        if (!vendorScript) vendorScript = new Promise((resolve, reject) => {
            const script = env.document.createElement('script');
            script.src = VENDOR + 'reader.js'; script.integrity = SCRIPT_SRI; script.crossOrigin = 'anonymous';
            script.onload = () => env.ZXingWASM ? resolve(env.ZXingWASM) : reject(new Error('decoder'));
            script.onerror = () => { vendorScript = null; script.remove(); reject(new Error('decoder')); };
            env.document.head.appendChild(script);
        });
        return vendorScript;
    }
    async function fallbackDecoder(env, canvas, signal) {
        if (!env.WebAssembly || !env.crypto?.subtle) throw new Error('decoder');
        const lib = await loadVendor(env);
        if (lib.ZXING_WASM_VERSION !== '3.1.5' || lib.ZXING_WASM_SHA256 !== WASM_SHA256) throw new Error('decoder');
        const response = await env.fetch(VENDOR + 'zxing_reader.wasm', { credentials: 'same-origin', signal });
        if (!response.ok) throw new Error('decoder');
        const binary = new Uint8Array(await response.arrayBuffer());
        const hash = Array.from(new Uint8Array(await env.crypto.subtle.digest('SHA-256', binary)), b => b.toString(16).padStart(2, '0')).join('');
        if (hash !== WASM_SHA256 || signal.aborted) throw new Error('decoder');
        // Provide bytes directly: the vendored library's default CDN URL is never used.
        await lib.prepareZXingModule({ overrides: { wasmBinary: binary, locateFile: () => VENDOR + 'zxing_reader.wasm' }, fireImmediately: true });
        return {
            async detect(video) {
                const scale = Math.min(1, 960 / Math.max(video.videoWidth, video.videoHeight));
                canvas.width = Math.max(1, Math.round(video.videoWidth * scale)); canvas.height = Math.max(1, Math.round(video.videoHeight * scale));
                const context = canvas.getContext('2d', { willReadFrequently: true });
                context.drawImage(video, 0, 0, canvas.width, canvas.height);
                const pixels = context.getImageData(0, 0, canvas.width, canvas.height);
                try {
                    return (await lib.readBarcodes(pixels, { formats: FALLBACK_FORMATS, maxNumberOfSymbols: 1, tryHarder: true, textMode: 'Plain' })).map(item => ({ rawValue: item.text }));
                } finally { pixels.data.fill(0); context.clearRect(0, 0, canvas.width, canvas.height); }
            },
            dispose() { lib.purgeZXingModule(); }
        };
    }
    class CameraSession {
        constructor({ video, onValue, onStatus = () => {}, onDevices = () => {}, env = root, createFallback = fallbackDecoder }) {
            Object.assign(this, { video, onValue, onStatus, onDevices, env, createFallback });
            this.generation = 0; this.latched = false; this.stream = null; this.timer = null;
            this.canvas = env.document.createElement('canvas'); this.decoder = null; this.pending = false; this.active = false;
        }
        reset() { this.latched = false; }
        stop(status = 'stopped') {
            ++this.generation; this.active = false; this.pending = false;
            if (this.timer !== null) this.env.clearTimeout(this.timer);
            this.timer = null; this.abort?.abort(); this.abort = null;
            stopTracks(this.stream); this.stream = null;
            this.video.pause?.(); this.video.srcObject = null;
            this.decoder?.dispose?.(); this.decoder = null;
            this.canvas.width = 0; this.canvas.height = 0;
            if (status) this.onStatus(status);
        }
        async start(deviceId = '') {
            if (this.latched) { this.onStatus('scanNextFirst'); return; }
            this.stop(null);
            if (!this.env.isSecureContext) { this.onStatus('https'); return; }
            if (!this.env.navigator?.mediaDevices?.getUserMedia) { this.onStatus('unsupported'); return; }
            const generation = this.generation;
            this.abort = new this.env.AbortController(); const abort = this.abort;
            this.pending = true; this.onStatus('permission');
            try {
                // Only called by the explicit Start button. Audio is NEVER requested.
                const stream = await this.env.navigator.mediaDevices.getUserMedia({ audio: false, video: deviceId ? { deviceId: { exact: deviceId } } : { facingMode: { ideal: 'environment' } } });
                if (generation !== this.generation) { stopTracks(stream); return; }
                this.stream = stream; this.video.srcObject = stream;
                await this.video.play();
                if (generation !== this.generation) { stopTracks(stream); return; }
                let detector;
                if (typeof this.env.BarcodeDetector === 'function') {
                    try {
                        const formats = await this.env.BarcodeDetector.getSupportedFormats();
                        if (NATIVE_FORMATS.every(format => formats.includes(format))) detector = new this.env.BarcodeDetector({ formats: NATIVE_FORMATS });
                        // A native QR-only detector must not silently lose requested product barcodes.
                    } catch { /* Limited / failed native implementation: use pinned local fallback. */ }
                }
                if (!detector) detector = await this.createFallback(this.env, this.canvas, abort.signal);
                if (generation !== this.generation) { detector?.dispose?.(); stopTracks(stream); return; }
                this.decoder = detector; this.pending = false; this.active = true; this.onStatus('active');
                this.env.navigator.mediaDevices.enumerateDevices?.().then(devices => {
                    if (generation === this.generation) this.onDevices(devices.filter(item => item.kind === 'videoinput'));
                }).catch(() => {});
                this.loop(generation);
            } catch (error) {
                if (generation !== this.generation) return;
                this.stop(null); this.onStatus(cameraError(error));
            }
        }
        async loop(generation) {
            if (generation !== this.generation || !this.active || this.latched) return;
            try {
                if (this.video.readyState >= 2 && this.video.videoWidth) {
                    let values;
                    try { values = await this.decoder.detect(this.video); }
                    catch (error) {
                        if (generation !== this.generation) return;
                        // Native detect can exist but fail at runtime. Switch once, in-browser.
                        if (this.decoder.nativeFallbackUsed || this.decoder.dispose) throw error;
                        this.decoder = await this.createFallback(this.env, this.canvas, this.abort.signal);
                        if (generation !== this.generation) { this.decoder?.dispose?.(); return; }
                        this.decoder.nativeFallbackUsed = true;
                        values = await this.decoder.detect(this.video);
                    }
                    if (generation !== this.generation) return;
                    const value = values?.find(item => typeof item.rawValue === 'string' && item.rawValue.length > 0)?.rawValue;
                    if (value) {
                        if (value.length > 128 || /[\x00-\x1f\x7f]/.test(value)) { this.latched = true; this.stop('invalid'); return; }
                        this.latched = true; this.stop('detected');
                        this.onValue(value); // Read-only preview; no add/post callback is connected here.
                        return;
                    }
                }
            } catch (error) {
                if (generation === this.generation) { this.stop(null); this.onStatus('decoder'); }
                return;
            }
            if (generation === this.generation) this.timer = this.env.setTimeout(() => this.loop(generation), 180);
        }
    }
    return { CameraSession, WASM_SHA256, SCRIPT_SRI, NATIVE_FORMATS, FALLBACK_FORMATS };
});
