#!/usr/bin/env python3
"""Build reproducible UPM and legacy Unity archives. Python standard library only."""
import argparse
import gzip
import io
import json
from pathlib import Path
import re
import tarfile
import uuid


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'com.anvelop.hexgrid/' + path).hex


def meta_for(path, folder=False):
    text = 'fileFormatVersion: 2\nguid: ' + guid(path) + '\n'
    if folder:
        text += 'folderAsset: yes\n'
    importer = 'DefaultImporter'
    if not folder and path.endswith('.cs'):
        importer = 'MonoImporter'
    elif not folder and path.endswith('.asmdef'):
        importer = 'AssemblyDefinitionImporter'
    text += importer + ':\n  externalObjects: {}\n'
    if importer == 'MonoImporter':
        text += '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
    return (text + '  userData: \n  assetBundleName: \n  assetBundleVariant: \n').encode()


def write_archive(path, entries):
    with path.open('wb') as raw:
        with gzip.GzipFile(filename='', mode='wb', fileobj=raw, mtime=0) as zipped:
            with tarfile.open(mode='w', fileobj=zipped, format=tarfile.USTAR_FORMAT) as archive:
                for name, data in sorted(entries.items()):
                    info = tarfile.TarInfo(name)
                    info.size = len(data)
                    info.mode = 0o644
                    archive.addfile(info, io.BytesIO(data))


def build(root, output):
    root, output = root.resolve(), output.resolve()
    if root == output:
        raise ValueError('Output must be separate from the package root')
    package = json.loads((root / 'package.json').read_text())
    stem = package['name'] + '-' + package['version']
    output.mkdir(parents=True, exist_ok=True)
    files = {}
    for path in root.rglob('*'):
        relative = path.relative_to(root)
        if (not path.is_file() or path.is_relative_to(output)
                or any(part.startswith('.') or part == '__pycache__' for part in relative.parts)
                or path.suffix == '.pyc'):
            continue
        files[relative.as_posix()] = path.read_bytes()
    upm = output / (stem + '.tgz')
    write_archive(upm, {'package/' + name: data for name, data in files.items()})

    # Legacy import includes managed examples in a visible Unity folder. Burst samples/tests stay in UPM/source.
    legacy = {}
    selected = {name: data for name, data in files.items()
                if not name.endswith('.meta') and (name.startswith('Runtime/')
                or name.startswith('Documentation~/') or name.startswith('Skills~/')
                or name.startswith('Samples~/ManagedUsage/') or name == 'README.md')}
    folders = {'Assets/Anvelop', 'Assets/Anvelop/HexGrid'}
    for name in selected:
        installed_name = name.replace('Samples~/ManagedUsage/', 'Samples/ManagedUsage/', 1)
        parent = Path(installed_name).parent
        while parent != Path('.'):
            folders.add('Assets/Anvelop/HexGrid/' + parent.as_posix())
            parent = parent.parent
    for name in sorted(folders):
        relative = name.removeprefix('Assets/Anvelop/HexGrid/')
        meta = files.get(relative + '.meta', meta_for(name, folder=True))
        key = re.search(rb'^guid: ([0-9a-f]{32})$', meta, re.M).group(1).decode()
        legacy[key + '/asset'] = b''
        legacy[key + '/asset.meta'] = meta
        legacy[key + '/pathname'] = name.encode()
    for name, data in selected.items():
        meta = files.get(name + '.meta', meta_for(name))
        key = re.search(rb'^guid: ([0-9a-f]{32})$', meta, re.M).group(1).decode()
        legacy[key + '/asset'] = data
        legacy[key + '/asset.meta'] = meta
        installed_name = name.replace('Samples~/ManagedUsage/', 'Samples/ManagedUsage/', 1)
        legacy[key + '/pathname'] = ('Assets/Anvelop/HexGrid/' + installed_name).encode()
    old = output / ('Anvelop.HexGrid-' + package['version'] + '.unitypackage')
    write_archive(old, legacy)
    print(upm.resolve())
    print(old.resolve())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    build(Path(__file__).resolve().parents[1], args.output.resolve())
