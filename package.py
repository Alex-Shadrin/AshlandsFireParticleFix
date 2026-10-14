"""Build and validate a Thunderstore archive from an explicit allowlist."""
from pathlib import Path
import hashlib
import json
import re
import struct
import zipfile

ROOT = Path(__file__).resolve().parent
manifest = json.loads((ROOT / 'manifest.json').read_text(encoding='utf-8'))
assert re.fullmatch(r'[A-Za-z0-9_]{1,128}', manifest['name'])
assert re.fullmatch(r'\d+\.\d+\.\d+', manifest['version_number'])
assert 0 < len(manifest['description']) <= 250
assert isinstance(manifest['website_url'], str)
assert manifest['dependencies'] == ['denikson-BepInExPack_Valheim-5.4.2351']
icon = (ROOT / 'icon.png').read_bytes()
assert icon[:8] == b'\x89PNG\r\n\x1a\n'
assert struct.unpack('>II', icon[16:24]) == (256, 256)
source = (ROOT / 'src' / 'AshlandsFireGuard.cs').read_text(encoding='utf-8')
assert 'GetWorldName() != "' not in source
assert '"Update"' not in source
assert 'PluginGuid' in source and f'"{manifest["version_number"]}"' in source
for filename in ['README.md', 'CHANGELOG.md', 'manifest.json', 'LICENSE', 'build.ps1']:
    (ROOT / filename).read_text(encoding='utf-8')
entries = {
    'manifest.json': ROOT / 'manifest.json',
    'README.md': ROOT / 'README.md',
    'CHANGELOG.md': ROOT / 'CHANGELOG.md',
    'LICENSE': ROOT / 'LICENSE',
    'icon.png': ROOT / 'icon.png',
    'BepInEx/plugins/AshlandsFireParticleFix/AshlandsFireGuard.dll': ROOT / 'build' / 'AshlandsFireGuard.dll',
}
out = ROOT / f"{manifest['name']}-{manifest['version_number']}.zip"
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as archive:
    for name, path in entries.items():
        assert path.is_file(), path
        archive.writestr(name, path.read_bytes())
with zipfile.ZipFile(out) as archive:
    assert archive.testzip() is None
    assert set(archive.namelist()) == set(entries)
    assert archive.read('manifest.json') == (ROOT / 'manifest.json').read_bytes()
    assert archive.read('BepInEx/plugins/AshlandsFireParticleFix/AshlandsFireGuard.dll') == (ROOT / 'build' / 'AshlandsFireGuard.dll').read_bytes()
    for name in archive.namelist():
        assert not any(part in name.lower() for part in ['backup', 'logoutput', '.fch', '.chunk']), name
        assert not name.lower().endswith('probe.dll'), name
        if name.endswith(('.cs', '.md', '.ps1', '.json')):
            text = archive.read(name).decode('utf-8')
            assert re.search(r'[A-Z]:\\Users\\[^\\]+', text) is None, name
source_entries = {
    '.gitignore': ROOT / '.gitignore',
    '.gitattributes': ROOT / '.gitattributes',
    'src/AshlandsFireGuard.cs': ROOT / 'src' / 'AshlandsFireGuard.cs',
    'tests/NativeGuardProbe.cs': ROOT / 'tests' / 'NativeGuardProbe.cs',
    'build.ps1': ROOT / 'build.ps1',
    'package.py': ROOT / 'package.py',
    'manifest.json': ROOT / 'manifest.json',
    'README.md': ROOT / 'README.md',
    'CHANGELOG.md': ROOT / 'CHANGELOG.md',
    'TESTING.md': ROOT / 'TESTING.md',
    'LICENSE': ROOT / 'LICENSE',
    'icon.png': ROOT / 'icon.png',
}
source_out = ROOT / f"{manifest['name']}-{manifest['version_number']}-source.zip"
with zipfile.ZipFile(source_out, 'w', zipfile.ZIP_DEFLATED) as archive:
    for name, path in source_entries.items():
        archive.writestr(name, path.read_bytes())
with zipfile.ZipFile(source_out) as archive:
    assert archive.testzip() is None
    assert set(archive.namelist()) == set(source_entries)
report = {
    'archive': str(out),
    'bytes': out.stat().st_size,
    'sha256': hashlib.sha256(out.read_bytes()).hexdigest(),
    'contents': list(entries),
    'manifest_validated': True,
    'icon_size': [256, 256],
    'private_saves_or_logs_included': False,
    'source_archive': str(source_out),
    'runtime_package_contains_development_files': False,
}
(ROOT / 'package-validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
