"""Prepare GitHub release assets only when explicitly publishing a release."""
import argparse, hashlib, json, re, shutil
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('--payload',required=True,type=Path);p.add_argument('--version',required=True)
p.add_argument('--tag',required=True);p.add_argument('--output',required=True,type=Path)
p.add_argument('--voice-asar',type=Path)
a=p.parse_args()
if not re.fullmatch(r'\d+\.\d+\.\d+',a.version) or not re.fullmatch(r'[A-Za-z0-9._-]+',a.tag):p.error('Use a numeric stable version and a simple release tag')
a.output.mkdir(parents=True,exist_ok=True)
files=[]
def add(source,target,name):
    content=source.read_bytes();sha=hashlib.sha256(content).hexdigest();asset=sha+'.bin'
    shutil.copyfile(source,a.output/asset)
    files.append(dict(target=target,path=name,size=len(content),sha256=sha,url=f'https://github.com/Oly132/among-us-custom-roles/releases/download/{a.tag}/{asset}'))
for source in sorted(a.payload.rglob('*')):
    if not source.is_file():continue
    name=source.relative_to(a.payload).as_posix()
    allowed=(name=='BepInEx/plugins/Forger.dll' or re.fullmatch(r'BepInEx/(core|unity-libs)/.+\.(dll|json|zip)',name) or re.fullmatch(r'dotnet/.+\.(dll|json)',name) or name in ('winhttp.dll','doorstop_config.ini','.doorstop_version') or re.fullmatch(r'ForgerSetup/(Voice|Updater)/.+\.(js|mjs|exe|dll|json)',name))
    if allowed:add(source,'game',name)
if not any(f['path']=='BepInEx/plugins/Forger.dll' for f in files):p.error('Payload must contain the clean Forger.dll')
if a.voice_asar:add(a.voice_asar,'voice','resources/app.asar')
manifest=dict(schema=1,repository='Oly132/among-us-custom-roles',version=a.version,gameAssemblySha256='615a95c9e09b6ffd45570883cb8459311c2b457533a38b37ed1704d6205aedf9',files=files)
(a.output/'update-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(f'Prepared {len(files)} managed file entries. No GitHub release was published.')
