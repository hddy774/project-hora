"""Read the AssemblyInformationalVersion custom attribute without loading/executing an assembly."""
from pathlib import Path
import hashlib
import json
import struct
import sys


def version(path):
    data = Path(path).read_bytes()
    u16 = lambda offset: struct.unpack_from('<H', data, offset)[0]
    u32 = lambda offset: struct.unpack_from('<I', data, offset)[0]
    pe = u32(0x3c)
    assert data[pe:pe + 4] == b'PE\0\0'
    optional = pe + 24
    directories = optional + (112 if u16(optional) == 0x20b else 96)
    sections = optional + u16(pe + 20)

    def raw(rva):
        for index in range(u16(pe + 6)):
            entry = sections + 40 * index
            address, size, offset = u32(entry + 12), max(u32(entry + 8), u32(entry + 16)), u32(entry + 20)
            if address <= rva < address + size:
                return offset + rva - address
        raise ValueError('RVA is outside PE sections')

    metadata = raw(u32(raw(u32(directories + 14 * 8)) + 8))
    assert data[metadata:metadata + 4] == b'BSJB'
    header = metadata + 16 + ((u32(metadata + 12) + 3) & ~3)
    position, streams = header + 4, {}
    for _ in range(u16(header + 2)):
        end = data.index(b'\0', position + 8)
        name = data[position + 8:end].decode()
        streams[name] = metadata + u32(position)
        position += 8 + ((end - (position + 8) + 1 + 3) & ~3)
    tables = streams.get('#~', streams.get('#-'))
    assert tables is not None
    heaps = data[tables + 6]
    string_size, guid_size, blob_size = (4 if heaps & flag else 2 for flag in (1, 2, 4))
    valid = struct.unpack_from('<Q', data, tables + 8)[0]
    rows, position = {}, tables + 24
    for table in range(64):
        if valid & (1 << table):
            rows[table] = u32(position)
            position += 4

    def index(table):
        return 4 if rows.get(table, 0) >= 65536 else 2

    def coded(bits, references):
        return 4 if max((rows.get(table, 0) for table in references), default=0) >= 1 << (16 - bits) else 2

    resolution = coded(2, (0, 26, 35, 1))
    type_ref = coded(2, (2, 1, 27))
    member_parent = coded(3, (2, 1, 26, 6, 27))
    attribute_parent = coded(5, (6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32, 35, 38, 39, 40, 42, 44, 43))
    attribute_type = coded(3, (6, 10))
    sizes = {
        0: 2 + string_size + 3 * guid_size,
        1: resolution + 2 * string_size,
        2: 4 + 2 * string_size + type_ref + index(4) + index(6),
        3: index(4), 4: 2 + string_size + blob_size, 5: index(6),
        6: 8 + string_size + blob_size + index(8), 7: index(8), 8: 4 + string_size,
        9: index(2) + type_ref, 10: member_parent + string_size + blob_size,
        11: 2 + coded(2, (4, 8, 23)) + blob_size,
        12: attribute_parent + attribute_type + blob_size,
    }
    offsets = {}
    for table in range(13):
        offsets[table] = position
        position += rows.get(table, 0) * sizes[table]

    def number(offset, size):
        return int.from_bytes(data[offset:offset + size], 'little')

    def text(value):
        start = streams['#Strings'] + value
        return data[start:data.index(b'\0', start)].decode()

    def compressed(offset):
        first = data[offset]
        if first < 0x80:
            return first, offset + 1
        if first < 0xc0:
            return ((first & 0x3f) << 8) | data[offset + 1], offset + 2
        return ((first & 0x1f) << 24) | int.from_bytes(data[offset + 1:offset + 4], 'big'), offset + 4

    found = []
    for row in range(rows.get(12, 0)):
        offset = offsets[12] + row * sizes[12]
        parent = number(offset, attribute_parent)
        constructor = number(offset + attribute_parent, attribute_type)
        if parent != (1 << 5 | 14) or constructor & 7 != 3:
            continue
        member = offsets[10] + ((constructor >> 3) - 1) * sizes[10]
        owner = number(member, member_parent)
        if owner & 7 != 1:
            continue
        reference = offsets[1] + ((owner >> 3) - 1) * sizes[1] + resolution
        if text(number(reference, string_size)) != 'AssemblyInformationalVersionAttribute' or text(number(reference + string_size, string_size)) != 'System.Reflection':
            continue
        blob = streams['#Blob'] + number(offset + attribute_parent + attribute_type, blob_size)
        _, payload = compressed(blob)
        assert u16(payload) == 1
        length, value = compressed(payload + 2)
        found.append(data[value:value + length].decode())
    assert len(found) == 1, (str(path), found)
    return {'path': str(path), 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(), 'assemblyInformationalVersion': found[0]}


if __name__ == '__main__':
    print(json.dumps([version(path) for path in sys.argv[1:]], ensure_ascii=False))
