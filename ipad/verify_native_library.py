"""Validate the build host's NativeAOT Mach-O output; no game or signing inputs."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess

EXPECTED={"forger_rules_abi","forger_rules_create","forger_rules_destroy","forger_rules_event","forger_rules_snapshot"}

def verify(path:Path,platform_name:str)->dict:
    b=path.read_bytes()
    if len(b)<32 or b[:4]!=b"\xcf\xfa\xed\xfe":raise ValueError("Expected a 64-bit native Mach-O library")
    _,cpu,_,kind,ncommands,command_size,_,_=struct.unpack_from("<8I",b)
    if cpu!=0x100000c or kind!=6:raise ValueError("Expected an ARM64 dynamic library")
    at=32;platform_id=None
    if command_size>len(b)-32 or ncommands>4096:raise ValueError("Invalid native load commands")
    for _ in range(ncommands):
        if at+8>32+command_size:raise ValueError("Truncated native load command")
        cmd,size=struct.unpack_from("<II",b,at)
        if size<8 or at+size>32+command_size:raise ValueError("Invalid native load command")
        if cmd==0x32:
            if size<24:raise ValueError("Truncated build version")
            platform_id=struct.unpack_from("<I",b,at+8)[0]
        elif cmd==0x25 and platform_id is None:platform_id=2
        at+=size
    expected_platform=2 if platform_name=="ios" else 1
    if platform_id!=expected_platform:raise ValueError(f"Wrong Apple platform {platform_id}; expected {expected_platform}")
    names=subprocess.check_output(["xcrun","nm","-gjU",str(path)],text=True)
    actual={line.strip().removeprefix("_") for line in names.splitlines()}
    if not EXPECTED<=actual:raise ValueError("Missing native exports: "+str(sorted(EXPECTED-actual)))
    return {"schema":"forger.ipad-native-library.v1","platform":platform_name,"architecture":"arm64","sha256":hashlib.sha256(b).hexdigest(),"bytes":len(b),"exports":sorted(EXPECTED),"installableApp":False,"gameplayTested":False}

if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("library",type=Path);p.add_argument("--platform",choices=["ios","macos"],required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    try:a.output.write_text(json.dumps(verify(a.library,a.platform),indent=2)+"\n")
    except (OSError,ValueError,subprocess.CalledProcessError) as e:p.exit(2,str(e)+"\n")
