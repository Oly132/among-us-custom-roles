"""Read-only iPad port preflight. This is not a loader, patcher or IPA packager."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import platform
import plistlib
import shutil
import struct
import subprocess


def macho_info(data: bytes) -> dict:
    if len(data) < 32 or data[:4] != b"\xcf\xfa\xed\xfe":
        raise ValueError("Expected a thin 64-bit Mach-O framework, not a Windows DLL or Android library")
    _, cpu, subtype, file_type, commands, command_size, flags, reserved = struct.unpack_from("<8I", data)
    if cpu != 0x0100000C:
        raise ValueError("Expected device ARM64 architecture")
    if commands > 4096 or command_size > len(data) - 32:
        raise ValueError("Malformed Mach-O command table")
    offset = 32
    encrypted = False
    for _ in range(commands):
        if offset + 8 > 32 + command_size:
            raise ValueError("Truncated Mach-O command")
        command, size = struct.unpack_from("<2I", data, offset)
        if size < 8 or offset + size > 32 + command_size:
            raise ValueError("Invalid Mach-O command length")
        if command in (0x21, 0x2C):
            if size < 20:
                raise ValueError("Invalid encryption-info command")
            encrypted |= struct.unpack_from("<I", data, offset + 16)[0] != 0
        offset += size
    if offset != 32 + command_size:
        raise ValueError("Mach-O command table does not match its declared size")
    return {"architecture": "arm64", "encrypted": encrypted, "sha256": hashlib.sha256(data).hexdigest()}


def game_info(app: Path) -> dict:
    app = app.resolve()
    if app.suffix != ".app" or not app.is_dir():
        raise ValueError("Pass a local, legitimately obtained iOS game .app directory")
    info = plistlib.loads((app / "Info.plist").read_bytes())
    framework = app / "Frameworks/UnityFramework.framework/UnityFramework"
    native = macho_info(framework.read_bytes())
    if native["encrypted"]:
        raise ValueError("The supplied framework is encrypted; this tool cannot use or decrypt it. A compatible development/modding build is required")
    metadata = (app / "Data/Managed/Metadata/global-metadata.dat").read_bytes()
    if len(metadata) < 8 or struct.unpack_from("<I", metadata)[0] != 0xFAB11BAF:
        raise ValueError("Missing or invalid IL2CPP metadata")
    return {"version": info.get("CFBundleShortVersionString"), "build": info.get("CFBundleVersion"),
            "framework": native, "metadataVersion": struct.unpack_from("<I", metadata, 4)[0],
            "metadataSha256": hashlib.sha256(metadata).hexdigest(), "compatibilityValidated": False}


def inspect(app: Path | None = None) -> dict:
    is_mac = platform.system() == "Darwin"
    sdk = None
    if is_mac and shutil.which("xcrun"):
        result = subprocess.run(["xcrun", "--sdk", "iphoneos", "--show-sdk-version"], capture_output=True, text=True)
        if result.returncode == 0:
            sdk = result.stdout.strip()
    return {"schema": "forger.ipad-preflight.v1", "host": platform.system(),
            "appleDeviceSdk": sdk, "appleBuildHostAvailable": is_mac and sdk is not None,
            "game": game_info(app) if app else None,
            "fullNativeGameAdapterImplemented": False, "signingInputsProvided": False,
            "installableIpadAppProduced": False}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-app", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        report = inspect(args.game_app)
    except (OSError, ValueError, plistlib.InvalidFileException) as error:
        parser.exit(2, f"Preflight failed: {error}\n")
    text = json.dumps(report, indent=2) + "\n"
    if args.output:
        args.output.write_text(text)
    else:
        print(text, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
