"""Package the built local Visual Studio client and common DV LSP transport."""
import argparse
from pathlib import Path
import zipfile

HERE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument("--out", required=True, type=Path)
args = parser.parse_args()
dll = HERE / "bin/Release/net472/DuelView.VisualStudio.dll"
if not dll.exists():
    raise SystemExit("Build DuelView.VisualStudio.csproj in Release first")
args.out.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(args.out, "w", zipfile.ZIP_DEFLATED) as archive:
    archive.write(dll, dll.name)
    archive.write(HERE / "extension.vsixmanifest", "extension.vsixmanifest")
    archive.write(HERE.parents[1] / "dv_lsp.py", "server/dv_lsp.py")
    archive.writestr("[Content_Types].xml", '''<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="vsixmanifest" ContentType="text/xml" />
<Default Extension="dll" ContentType="application/octet-stream" />
<Default Extension="py" ContentType="text/plain" />
</Types>''')
print(args.out.resolve())
