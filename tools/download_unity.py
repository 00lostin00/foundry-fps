"""Download pinned official Unity installers. Never stores account credentials."""
import hashlib
import json
from pathlib import Path
import requests

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / '.tools'
DEST.mkdir(exist_ok=True)
VERSION = '6000.3.28f1'

def download(url, name, expected=None):
    target = DEST / name
    partial = target.with_suffix(target.suffix + '.part')
    if target.exists() and (expected is None or target.stat().st_size == expected):
        print(f'Already downloaded: {name}', flush=True)
        return target
    offset = partial.stat().st_size if partial.exists() else 0
    headers = {'Range': f'bytes={offset}-'} if offset else {}
    with requests.get(url, headers=headers, stream=True, timeout=(30, 120)) as r:
        r.raise_for_status()
        if r.status_code != 206:
            offset = 0
        print(f'Downloading {name}, resume={offset // 1048576} MiB', flush=True)
        progress = offset
        checkpoint = progress
        with partial.open('ab' if offset else 'wb') as f:
            for chunk in r.iter_content(4 * 1024 * 1024):
                f.write(chunk)
                progress += len(chunk)
                if progress - checkpoint > 128 * 1024 * 1024:
                    print(f'{name}: {progress // 1048576} MiB', flush=True)
                    checkpoint = progress
    if expected and partial.stat().st_size != expected:
        raise RuntimeError(f'Incomplete download: {name}')
    partial.replace(target)
    print(f'Complete: {name}; SHA256={hashlib.file_digest(target.open("rb"), "sha256").hexdigest()}', flush=True)
    return target

if __name__ == '__main__':
    api = f'https://services.api.unity.com/unity/editor/release/v1/releases?version={VERSION}&architecture=X86_64&platform=WINDOWS'
    response = requests.get(api, timeout=30)
    response.raise_for_status()
    release = response.json()['results'][0]
    (DEST / 'unity-release.json').write_text(json.dumps(release, indent=2), encoding='utf-8')
    editor = release['downloads'][0]
    download('https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup-x64.exe', 'UnityHubSetup-x64.exe')
    download(editor['url'], f'UnitySetup64-{VERSION}.exe', editor['downloadSize']['value'])
    for module in editor['modules']:
        if module['id'] in ('linux-mono',):
            download(module['url'], f'UnitySetup-{module["id"]}-{VERSION}.exe', module['downloadSize']['value'])
