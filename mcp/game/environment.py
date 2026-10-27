"""Resolve or prepare the project's Godot .NET and .NET SDK environment."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import threading
import time
import urllib.request
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / ".runtime"
LOCK = threading.Lock()
VERSION = re.search(r'Godot.NET.Sdk/([^"\s]+)', (ROOT / "godot_csharp/DualOntologyWorldEngine.csproj").read_text()).group(1)


def command_output(command, deadline=None):
    timeout = 15 if deadline is None else max(0.001, min(15, deadline - time.monotonic()))
    if deadline is not None and time.monotonic() >= deadline: return ""
    try:
        result = subprocess.run([str(x) for x in command], capture_output=True, text=True,
                                encoding="utf-8", errors="replace", timeout=timeout,
                                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        return result.stdout.strip() if result.returncode == 0 else ""
    except (OSError, subprocess.TimeoutExpired):
        return ""


def resolve_dotnet(required=True, deadline=None):
    executable = "dotnet.exe" if os.name == "nt" else "dotnet"
    for candidate in [LOCAL / "dotnet" / executable, shutil.which("dotnet")]:
        if candidate and Path(candidate).is_file() and any(
                line.startswith("8.") for line in command_output([candidate, "--list-sdks"], deadline).splitlines()):
            return str(Path(candidate).resolve())
    if required:
        raise RuntimeError(".NET 8 SDK missing. Call dual-ontology-environment.prepare first.")
    return None


def godot_candidates():
    base = LOCAL / "godot" / VERSION
    if base.exists():
        yield from base.rglob("*_console.exe")
    yield os.environ.get("DV_GODOT")
    yield shutil.which(f"Godot_v{VERSION}-stable_mono_win64_console.exe")
    yield shutil.which("godot")


def resolve_godot(required=True, deadline=None):
    for candidate in godot_candidates():
        if not candidate or not Path(candidate).is_file():
            continue
        version = command_output([candidate, "--version"], deadline)
        if version.startswith(VERSION + ".") and ".mono." in version and (Path(candidate).parent / "GodotSharp").is_dir():
            return str(Path(candidate).resolve())
    if required:
        raise RuntimeError(f"Godot {VERSION} .NET missing. Call dual-ontology-environment.prepare first.")
    return None


def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "dual-ontology-environment/1.0"})
    return urllib.request.urlopen(request, timeout=30)


def download(url, destination, expected, algorithm):
    digest = hashlib.new(algorithm)
    with fetch(url) as response, destination.open("xb") as output:
        while chunk := response.read(1024 * 1024):
            digest.update(chunk)
            output.write(chunk)
    if digest.hexdigest().lower() != expected.lower():
        raise RuntimeError(f"{algorithm} checksum mismatch; archive was not installed: {destination}")


def unpack(archive, destination):
    destination.mkdir(parents=True)
    with zipfile.ZipFile(archive) as bundle:
        for item in bundle.infolist():
            target = (destination / item.filename).resolve()
            target.relative_to(destination.resolve())
            if item.filename.startswith(("/", "\\")) or (item.external_attr >> 16) & 0o170000 == 0o120000:
                raise ValueError("Archive contains an unsafe path or symbolic link")
        bundle.extractall(destination)


def prepare(local_copy=False):
    """Reuse a valid installation; if missing, install official binaries locally.

    local_copy requests a project-local Godot copy even if an external one exists.
    No global installation, environment-variable mutation, or recursive deletion.
    """
    if os.name != "nt" or os.environ.get("PROCESSOR_ARCHITECTURE", "AMD64").upper() not in ("AMD64", "X86_64"):
        raise RuntimeError("This project's environment installer supports Windows x64 only.")
    with LOCK:
        godot = resolve_godot(False)
        dotnet = resolve_dotnet(False)
        actions = []
        if not dotnet:
            LOCAL.mkdir(exist_ok=True)
            with fetch("https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json") as response:
                metadata = json.load(response)
            sdk = next(release["sdk"] for release in metadata["releases"] if release.get("sdk"))
            package = next(file for file in sdk["files"] if file["rid"] == "win-x64" and file["url"].endswith(".zip"))
            stage = LOCAL / ("install-dotnet-" + uuid.uuid4().hex)
            stage.mkdir()
            archive = stage / "sdk.zip"
            download(package["url"], archive, package["hash"], "sha512")
            unpack(archive, stage / "sdk")
            if not command_output([stage / "sdk/dotnet.exe", "--list-sdks"]):
                raise RuntimeError("Downloaded .NET SDK did not validate; staging files preserved.")
            (stage / "sdk").rename(LOCAL / "dotnet")
            archive.unlink()
            stage.rmdir()
            dotnet = resolve_dotnet()
            actions.append("installed-official-dotnet-8-sdk")
        target = LOCAL / "godot" / VERSION
        needs_copy = local_copy and godot and not Path(godot).is_relative_to(LOCAL)
        if not godot or needs_copy:
            if target.exists():
                raise RuntimeError(f"An incomplete local Godot directory exists; it was not overwritten: {target}")
            LOCAL.mkdir(exist_ok=True)
            stage = LOCAL / ("install-godot-" + uuid.uuid4().hex)
            stage.mkdir()
            distribution = stage / "distribution"
            if godot:
                source = Path(godot).parent
                distribution.mkdir()
                shutil.copy2(godot, distribution / Path(godot).name)
                regular = source / Path(godot).name.replace("_console.exe", ".exe")
                if regular.is_file(): shutil.copy2(regular, distribution / regular.name)
                shutil.copytree(source / "GodotSharp", distribution / "GodotSharp")
                actions.append("cloned-existing-mono-distribution")
            else:
                # Pinned to the project SDK, with the digest from the official release.
                if VERSION != "4.6.1":
                    raise RuntimeError("Update the verified Godot release checksum for the project's SDK version.")
                filename = f"Godot_v{VERSION}-stable_mono_win64.zip"
                url = f"https://github.com/godotengine/godot-builds/releases/download/{VERSION}-stable/{filename}"
                archive = stage / filename
                download(url, archive, "e10a41f33392f86e93557610611ead8722142913f9e0fab6a8148d1aa5b64c78", "sha256")
                unpack(archive, distribution)
                actions.append("downloaded-verified-official-mono-distribution")
            executable = next(distribution.rglob("*_console.exe"))
            version = command_output([executable, "--version"])
            if not version.startswith(VERSION + ".") or ".mono." not in version or not (executable.parent / "GodotSharp").is_dir():
                raise RuntimeError("Godot .NET validation failed; staging files preserved.")
            target.parent.mkdir(parents=True, exist_ok=True)
            distribution.rename(target)
            # Only explicitly known archive files and empty staging directories are removed.
            for file in stage.iterdir():
                if file.is_file(): file.unlink()
            stage.rmdir()
            godot = resolve_godot()
        result = {"ready": True, "godot": godot, "godot_version": VERSION, "csharp": True,
                  "dotnet": dotnet, "project_local_godot": Path(godot).is_relative_to(LOCAL), "actions": actions}
        LOCAL.mkdir(exist_ok=True)
        (LOCAL / "environment.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        return result
