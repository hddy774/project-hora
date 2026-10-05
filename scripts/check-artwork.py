from pathlib import Path
import hashlib, json, struct
root = Path(__file__).resolve().parents[1] / 'src/AlphaExchange.Android/Assets/portraits'
items = json.loads((root / 'manifest.json').read_text())
assert [x['id'] for x in items] == list(range(1, 201))
assert sum(x['gender'] == 'female' for x in items) == 140
assert sum(x['gender'] == 'male' for x in items) == 60
assert len({x['name'] for x in items}) == 200
assert len({(x['file'], x['x'], x['y'], x['width'], x['height']) for x in items}) == 200
assert all(x['age'] >= 25 for x in items)
for item in items:
    data = (root / item['file']).read_bytes()
    assert hashlib.sha256(data).hexdigest() == item['sha256']
    w, h = struct.unpack('>II', data[16:24])
    assert 0 <= item['x'] < w and 0 <= item['y'] < h
    assert item['width'] > 0 and item['height'] > 0
    assert item['x'] + item['width'] <= w and item['y'] + item['height'] <= h
assert len({x['sha256'] for x in items}) == 20
print('PASS: 200 unique portrait regions, 140 women / 60 men, 20 generated originals, bounds and SHA-256 verified.')
