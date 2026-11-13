"""Package Unity's entire Windows output and verify the ZIP's runtime files."""
import hashlib
import json
import struct
import shutil
import time
from pathlib import Path
import zipfile

root = Path(__file__).resolve().parents[1]
game = root / "Deliverables" / "Mossmere-Windows-x64-v3"
qa = root / "FINALS_SANDBOX" / "QA"
assert (qa / "playcheck-result.txt").read_text().startswith("PASS\n"), "Play mode tests must pass"
assert (qa / "build-result.txt").read_text().startswith("Succeeded"), "Windows build must pass"
assert (qa / "standalone-result.txt").read_text().startswith("PASS"), "Standalone check must pass"
assert "English HUD" in (qa / "standalone-result.txt").read_text(), "The revised player must be checked"
assert "villager greetings" in (qa / "standalone-result.txt").read_text(), "The dialogue update must be checked"
diagnostics=game/"Mossmere_BurstDebugInformation_DoNotShip"
if diagnostics.exists():
    destination=game.parent/f"Mossmere-BuildDiagnostics-v3-{time.time_ns()}"
    assert diagnostics.resolve().is_relative_to(game.parent.resolve())
    assert destination.resolve().is_relative_to(game.parent.resolve())
    shutil.move(str(diagnostics),str(destination))

(game / "READ-ME.txt").write_text(
    "MOSSMERE\n\n"
    "Extract this entire folder before starting Mossmere.exe. Keep all files and folders together.\n\n"
    "WASD or arrow keys: walk\nR or Return to Spawn button: return to village spawn\nM or Music button: toggle background music\nE or Next button: next villager dialogue\nEscape: quit\n\n"
    "Walk into a glowing cyan ring to return home. Nearby HUD guidance explains the beacon.\n"
    "Approach villagers to hear greetings and local stories. Walk away to close the conversation.\n"
    "Explore the village, woodland cottage, west meadow, northern lookout and lakeside pier.\n\n"
    "Art: Goldmetal, Top-Down 2D RPG Assets Pack v1.0, copyright 2021 Goldmetal.\n"
    "https://assetstore.unity.com/packages/2d/characters/top-down-2d-rpg-assets-pack-188718\n"
    "Music: Mossmere Meadow, original synthesized soundtrack created for this activity.\n"
    "Activity source: https://github.com/Tinenen-cs/SANBOX\n",
    encoding="utf-8",
)
required = [
    "Mossmere.exe", "UnityPlayer.dll", "UnityCrashHandler64.exe",
    "Mossmere_Data/boot.config", "Mossmere_Data/globalgamemanagers",
    "Mossmere_Data/level0", "Mossmere_Data/Managed/Assembly-CSharp.dll",
    "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll",
]
for name in required:
    assert (game / name).is_file(), f"Missing runtime file: {name}"
exe = (game / "Mossmere.exe").read_bytes()
pe = struct.unpack_from("<I", exe, 0x3C)[0]
assert exe[pe:pe + 4] == b"PE\0\0" and struct.unpack_from("<H", exe, pe + 4)[0] == 0x8664, "Executable must target Windows x64"
files = sorted(p for p in game.rglob("*") if p.is_file())
archive_path = game.with_suffix(".zip")
with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for path in files:
        archive.write(path, path.relative_to(game.parent).as_posix())
with zipfile.ZipFile(archive_path) as archive:
    assert archive.testzip() is None, "ZIP integrity check failed"
    assert len(archive.infolist()) == len(files), "ZIP did not include every game file"
    for path in files:
        assert archive.getinfo(path.relative_to(game.parent).as_posix()).file_size == path.stat().st_size
digest = hashlib.sha256(archive_path.read_bytes()).hexdigest()
result = {"result": "PASS", "zip": archive_path.relative_to(root).as_posix(), "bytes": archive_path.stat().st_size,
          "files": len(files), "architecture": "Windows x64 (AMD64)", "sha256": digest, "required_runtime_files_verified": required}
(qa / "package-result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
