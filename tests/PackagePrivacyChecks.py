"""Audit ZIP bytes and unpack UnityFS blocks; verify metadata-only DLL changes.

Uses only the Python standard library. Run with old ZIP, new ZIP, output JSON.
"""
import hashlib
import json
import lzma
import os
from pathlib import Path
import struct
import sys
import zipfile


def lz4(data):
    out = bytearray()
    p = 0
    while p < len(data):
        token = data[p]
        p += 1
        count = token >> 4
        if count == 15:
            while True:
                value = data[p]
                p += 1
                count += value
                if value != 255:
                    break
        out.extend(data[p:p + count])
        p += count
        if p == len(data):
            break
        distance = data[p] | data[p + 1] << 8
        p += 2
        count = token & 15
        if count == 15:
            while True:
                value = data[p]
                p += 1
                count += value
                if value != 255:
                    break
        for _ in range(count + 4):
            out.append(out[-distance])
    return bytes(out)


def decode(data, flags):
    mode = flags & 63
    if mode == 0:
        return data
    if mode in (2, 3):
        return lz4(data)
    if mode == 1:
        prop = data[0]
        filters = [{"id": lzma.FILTER_LZMA1,
                    "dict_size": struct.unpack_from('<I', data, 1)[0],
                    "lc": prop % 9, "lp": (prop // 9) % 5, "pb": prop // 45}]
        return lzma.decompress(data[5:], format=lzma.FORMAT_RAW, filters=filters)
    raise AssertionError('Unknown compression ' + str(mode))


def unpack_bundle(data):
    p = data.index(b'\0') + 1
    version = struct.unpack_from('>I', data, p)[0]
    p += 4
    for _ in range(2):
        p = data.index(b'\0', p) + 1
    total, packed, raw, flags = struct.unpack_from('>QIII', data, p)
    assert total == len(data)
    p += 20
    if version >= 7:
        p = (p + 15) & ~15
    info_pos = len(data) - packed if flags & 128 else p
    info = decode(data[info_pos:info_pos + packed], flags)
    assert len(info) == raw
    block_pos = p if flags & 128 else p + packed
    if flags & 512:
        block_pos = (block_pos + 15) & ~15
    count = struct.unpack_from('>I', info, 16)[0]
    q = 20
    result = bytearray(info)
    for _ in range(count):
        uncompressed, compressed, block_flags = struct.unpack_from('>IIH', info, q)
        q += 10
        block = decode(data[block_pos:block_pos + compressed], block_flags)
        assert len(block) == uncompressed
        result.extend(block)
        block_pos += compressed
    return bytes(result)


def debug_ranges(data):
    pe = struct.unpack_from('<I', data, 60)[0]
    optional = pe + 24
    directory = optional + (112 if struct.unpack_from('<H', data, optional)[0] == 523 else 96)
    sections = optional + struct.unpack_from('<H', data, pe + 20)[0]

    def offset(rva):
        for i in range(struct.unpack_from('<H', data, pe + 6)[0]):
            vs, va, rs, rp = struct.unpack_from('<IIII', data, sections + 40 * i + 8)
            if va <= rva < va + max(vs, rs):
                return rp + rva - va
        raise AssertionError('Invalid RVA')

    rva, size = struct.unpack_from('<II', data, directory + 48)
    start = offset(rva)
    ranges = []
    for p in range(start, start + size, 28):
        kind, length, _, raw = struct.unpack_from('<IIII', data, p + 12)
        if kind == 2:
            assert data[raw:raw + 4] == b'RSDS'
            ranges.append((raw + 24, raw + length))
    return ranges


def main():
    # The old ZIP lives under this task's output directory; derive its private
    # workspace label so this test itself contains no local identifying text.
    workspace_label = Path(sys.argv[1]).resolve().parent.parent.name
    identifiers = [os.environ['USERNAME'], os.environ.get('COMPUTERNAME', '__absent__'),
                   workspace_label]

    def check(data, label):
        for identifier in identifiers:
            for encoding in ('utf-8', 'utf-16le'):
                assert identifier.lower().encode(encoding) not in data.lower(), 'Identifier in ' + label

    with zipfile.ZipFile(sys.argv[1]) as old, zipfile.ZipFile(sys.argv[2]) as new:
        previous = {f.filename.split('/', 1)[1]: old.read(f) for f in old.infolist() if not f.is_dir()}
        changes = []
        bundles = []
        for f in new.infolist():
            if f.is_dir():
                continue
            data = new.read(f)
            relative = f.filename.split('/', 1)[1]
            check(data, relative)
            if data.startswith(b'UnityFS\0'):
                raw = unpack_bundle(data)
                check(raw, relative + ' decompressed')
                bundles.append({'name': relative, 'unpacked_bytes': len(raw)})
            if relative.startswith('payload/') and data != previous[relative]:
                before = previous[relative]
                assert len(before) == len(data)
                ranges = debug_ranges(before)
                assert all(any(lo <= i < hi for lo, hi in ranges)
                           for i, (a, b) in enumerate(zip(before, data)) if a != b)
                changes.append(relative)
        assert len(changes) == 12
        result = {'files_scanned': sum(not f.is_dir() for f in new.infolist()),
                  'personal_identifier_matches': 0, 'changed_payloads': len(changes),
                  'changes_confined_to_debug_paths': True, 'asset_bundles_decompressed': bundles,
                  'zip_sha256': hashlib.sha256(Path(sys.argv[2]).read_bytes()).hexdigest()}
        Path(sys.argv[3]).write_text(json.dumps(result, indent=2), encoding='utf-8')
        print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
