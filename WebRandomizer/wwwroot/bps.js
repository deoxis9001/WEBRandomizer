// JS port of RandomizerCore/Utilities/Util/BPSPatcher.cs and Models/Patch.cs
// Generates a BPS patch byte-for-byte identical to the C# implementation.
//
// Public API:
//   BPS.crc32(uint8) -> number (uint32)
//   BPS.generate(sourceUint8, patchedUint8) -> Uint8Array (the .bps file content)
//
// Linear-mode BPS only (matches the C# generator).

(function (global) {
    'use strict';

    const CRC_TABLE = (function () {
        const t = new Uint32Array(256);
        for (let n = 0; n < 256; n++) {
            let c = n >>> 0;
            for (let k = 0; k < 8; k++)
                c = (c & 1) ? (0xEDB88320 ^ (c >>> 1)) >>> 0 : c >>> 1;
            t[n] = c >>> 0;
        }
        return t;
    })();

    function crc32(bytes) {
        let c = 0xFFFFFFFF >>> 0;
        for (let i = 0; i < bytes.length; i++)
            c = (CRC_TABLE[(c ^ bytes[i]) & 0xFF] ^ (c >>> 8)) >>> 0;
        return (c ^ 0xFFFFFFFF) >>> 0;
    }

    function writeVln(data, out) {
        // Mirrors C#: while loop, post-decrement after recursive shift.
        // Inputs always non-negative here.
        while (true) {
            const b = data & 0x7F;
            data = Math.floor(data / 128);
            if (data === 0) {
                out.push((0x80 | b) & 0xFF);
                return;
            }
            out.push(b & 0xFF);
            data -= 1;
        }
    }

    function pushUint32LE(out, v) {
        out.push(v & 0xFF, (v >>> 8) & 0xFF, (v >>> 16) & 0xFF, (v >>> 24) & 0xFF);
    }

    // Mirrors BpsPatcher.ReadFlush
    function flushReads(actions, target, outputOffset, readLength) {
        if (readLength === 0) return;
        const start = outputOffset - readLength;
        const buf = new Uint8Array(readLength);
        for (let i = 0; i < readLength; i++) buf[i] = target[start + i];
        actions.push({ type: 1 /* TargetRead */, length: readLength, bytes: buf });
    }

    // Mirrors BpsPatcher.GeneratePatch (linear mode)
    function generate(source, target) {
        if (!(source instanceof Uint8Array) || !(target instanceof Uint8Array))
            throw new TypeError('generate(source, target): both must be Uint8Array');

        const actions = [];
        let relativeOffset = 0;
        let outputOffset = 0;
        let readLength = 0;

        const sLen = source.length;
        const tLen = target.length;
        const minLen = Math.min(sLen, tLen);

        while (outputOffset < tLen) {
            // Count contiguous matches against source starting at outputOffset
            let sourceLength = 0;
            for (let i = 0; outputOffset + i < minLen; i++) {
                if (source[outputOffset + i] !== target[outputOffset + i]) break;
                sourceLength++;
            }

            // Count run of identical bytes in target starting at outputOffset
            let relLength = 0;
            const baseByte = target[outputOffset];
            for (let i = 1; outputOffset + i < tLen; i++) {
                if (baseByte !== target[outputOffset + i]) break;
                relLength++;
            }

            if (relLength >= 4) {
                readLength++;
                outputOffset++;
                flushReads(actions, target, outputOffset, readLength);
                readLength = 0;

                const relOff = outputOffset - 1 - relativeOffset;
                actions.push({ type: 3 /* TargetCopy */, length: relLength, relOffset: relOff });
                outputOffset += relLength;
                relativeOffset = outputOffset - 1;
            } else if (sourceLength >= 4) {
                flushReads(actions, target, outputOffset, readLength);
                readLength = 0;
                actions.push({ type: 0 /* SourceRead */, length: sourceLength });
                outputOffset += sourceLength;
            } else {
                readLength++;
                outputOffset++;
            }
        }
        flushReads(actions, target, outputOffset, readLength);

        // Encode patch bytes (mirrors Patch.CreatePatchData)
        const out = [];
        out.push(0x42, 0x50, 0x53, 0x31); // "BPS1"
        writeVln(sLen, out);
        writeVln(tLen, out);
        out.push(0x80); // empty metadata

        for (const a of actions) {
            writeVln(((a.length - 1) << 2) + a.type, out);
            switch (a.type) {
                case 1: // TargetRead
                    for (let i = 0; i < a.bytes.length; i++) out.push(a.bytes[i]);
                    break;
                case 2: // SourceCopy
                case 3: // TargetCopy
                    writeVln((Math.abs(a.relOffset) << 1) + (a.relOffset < 0 ? 1 : 0), out);
                    break;
                case 0: // SourceRead — no payload
                    break;
            }
        }

        const sourceCrc = crc32(source);
        const patchedCrc = crc32(target);
        pushUint32LE(out, sourceCrc);
        pushUint32LE(out, patchedCrc);

        const partial = new Uint8Array(out);
        const patchCrc = crc32(partial);
        const final = new Uint8Array(out.length + 4);
        final.set(partial);
        final[out.length    ] =  patchCrc        & 0xFF;
        final[out.length + 1] = (patchCrc >>>  8) & 0xFF;
        final[out.length + 2] = (patchCrc >>> 16) & 0xFF;
        final[out.length + 3] = (patchCrc >>> 24) & 0xFF;
        return final;
    }

    global.BPS = { crc32, generate };
})(window);
