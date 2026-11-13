"""Create a real WinRAR SFX .exe and verify its extracted Windows game."""
from pathlib import Path
import hashlib
import json
import os
import subprocess

root=Path(__file__).resolve().parents[1]
deliverables=root/"Deliverables"
game=deliverables/"Mossmere-Windows-x64-v4"
qa=root/"FINALS_SANDBOX/QA"
rar=Path(os.environ.get("ProgramFiles",r"C:\Program Files"))/"WinRAR/Rar.exe"
assert rar.is_file(), "WinRAR is required to create the submission executable"
package=json.loads((qa/"package-result.json").read_text())
assert package["result"]=="PASS" and package["zip"].endswith(game.name+".zip")
archive=deliverables/"Mossmere-Canvas-Submission-v4.exe"
comment=deliverables/"Mossmere-SFX-Readme-v4.txt"
comment.write_text(
    "MOSSMERE - WINDOWS GAME\n\n"
    "This self-extracting archive contains the complete Windows x64 game.\n"
    "Choose a destination and extract all files. Then open\n"
    "Mossmere-Windows-x64-v4\\Mossmere.exe. The game opens full screen.\n\n"
    "WASD / arrows: Move | R: Return to spawn | M: Music on/off | Esc: Quit\n"
    "Glowing rings return you to the village. Approach villagers for greetings; E advances the story.\n",
    encoding="utf-8",
)

def run(args,cwd=deliverables,timeout=180):
    result=subprocess.run([str(a) for a in args],cwd=cwd,capture_output=True,
                          text=True,errors="replace",timeout=timeout)
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {result.stdout[-1500:]} {result.stderr[-1000:]}")
    return result

run([rar,"a","-r","-m5","-sfx","-t","-y",f"-z{comment}",archive,game.name])
assert archive.read_bytes()[:2]==b"MZ", "Submission must be a Windows executable, not a renamed ZIP"
run([rar,"t","-idq",archive])
extract=deliverables/"SFX-Extraction-Check-v4"
extract.mkdir(exist_ok=True)
assert extract.resolve().is_relative_to(deliverables.resolve())
run([rar,"x","-y","-idq",archive,str(extract)+os.sep])
extracted=extract/game.name
files=sorted(p for p in game.rglob("*") if p.is_file())
for source in files:
    other=extracted/source.relative_to(game)
    assert other.is_file(), f"Missing extracted runtime: {source.name}"
    assert hashlib.sha256(other.read_bytes()).digest()==hashlib.sha256(source.read_bytes()).digest(), f"Extracted content mismatch: {source.name}"
smoke=qa/"sfx-verified"
smoke.mkdir(exist_ok=True)
run([extracted/"Mossmere.exe","-batchmode","-verifyFullscreen","-sandboxSmoke","-smokeOutput",smoke,
     "-logFile",smoke/"player.log"],cwd=extracted,timeout=90)
assert (smoke/"standalone-result.txt").read_text().startswith("PASS"), "Extracted game smoke check failed"
assert (smoke/"fullscreen-result.txt").read_text().startswith("PASS"), "Extracted game must fill the monitor"
result={"result":"PASS","submission":archive.relative_to(root).as_posix(),
        "format":"WinRAR self-extracting Windows executable (SFX)","bytes":archive.stat().st_size,
        "files_verified":len(files),"sha256":hashlib.sha256(archive.read_bytes()).hexdigest(),
        "archive_integrity":"PASS","extracted_file_hashes":"PASS","extracted_game_launch":"PASS","native_fullscreen":"PASS"}
(qa/"canvas-submission-result.json").write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
print(json.dumps(result,indent=2))
