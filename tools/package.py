from pathlib import Path
import hashlib, json, shutil, zipfile
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT.parent/'outputs'
VERSION='0.1.0'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    OUT.mkdir(exist_ok=True)
    assembly=ROOT/'mod/1.7/Assemblies'
    assembly.mkdir(parents=True,exist_ok=True)
    for name in ['ResourceLedger.dll','ResourceLedger.Core.dll']:
        shutil.copy2(ROOT/'bin/Release/net10.0'/name,assembly/name)
    install=OUT/f'ResourceLedger_{VERSION}_Mod.zip'
    source=OUT/f'ResourceLedger_{VERSION}_Source.zip'
    with zipfile.ZipFile(install,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted((ROOT/'mod').rglob('*')):
            if p.is_file():z.write(p,'ResourceLedger/'+p.relative_to(ROOT/'mod').as_posix())
        for name in ['README.md','VALIDATION.md','LICENSE']:z.write(ROOT/name,'ResourceLedger/'+name)
    allowed={'src','tests','tools','mod','docs'}
    root_files={'ResourceLedger.csproj','Directory.Build.props','NuGet.Config','build.ps1','README.md','VALIDATION.md','REVIEW.md','LICENSE'}
    with zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(ROOT.rglob('*')):
            if not p.is_file() or p.is_symlink():continue
            rel=p.relative_to(ROOT);parts=rel.parts
            if any(x in {'bin','obj','work','__pycache__','.git'} for x in parts):continue
            if 'Assemblies' in parts:continue
            if pa…1044 tokens truncated…ding="utf-8"?><loadFolders><v1.7><li>/</li><li>1.7</li></v1.7></loadFolders>
