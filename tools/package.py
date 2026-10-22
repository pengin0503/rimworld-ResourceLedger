from pathlib import Path
import argparse, hashlib, json, shutil, zipfile
from build_manifest import version, verify as verify_prebuilt
ROOT=Path(__file__).resolve().parents[1]

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--prebuilt',action='store_true')
    parser.add_argument('--output',type=Path,default=ROOT.parent/'outputs')
    args=parser.parse_args();out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
    ver=version();assembly=ROOT/'mod/1.7/Assemblies';assembly.mkdir(parents=True,exist_ok=True)
    if args.prebuilt:verify_prebuilt()
    else:
        built=ROOT/'bin/Release/net10.0'
        verify_prebuilt(built,built/'build-info.json')
        for name in ['ResourceLedger.dll','ResourceLedger.Core.dll']:
            shutil.copy2(ROOT/'bin/Release/net10.0'/name,assembly/name)
        shutil.copy2(built/'build-info.json',ROOT/'mod/About/build-info.json')
    install=out/f'ResourceLedger_{ver}_Mod.zip';source=out/f'ResourceLedger_{ver}_Source.zip'
    with zipfile.ZipFile(install,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted((ROOT/'mod').rglob('*')):
            if p.is_file():z.write(p,'ResourceLedger/'+p.relative_to(ROOT/'mod').as_posix())
        for name in ['README.md','VALIDATION.md','LICENSE']:z.write(ROOT/name,'ResourceLedger/'+name)
    allowed={'src','tests','tools','mod','docs','.github'}
    root_files={'ResourceLedger.csproj','Directory.Build.props','NuGet.Config','build.ps1','README.md','VALIDATION.md','REVIEW.md','LICENSE','.gitignore','.gitattributes'}
    with zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(ROOT.rglob('*')):
            if not p.is_file() or p.is_symlink():continue
            rel=p.relative_to(ROOT);parts=rel.parts
            if any(x in {'bin','obj','work','__pycache__','.git'} for x in parts):continue
            if 'Assemblies' in parts or p.name=='build-info.json':continue
            if parts[0] in allowed or (len(parts)==1 and parts[0] in root_files):z.write(p,'ResourceLedgerSource/'+rel.as_posix())
    for archive in [install,source]:
        with zipfile.ZipFile(archive) as z:
            assert z.testzip() is None
            assert all(not n.startswith('/') and '..' not in Path(n).parts for n in z.namelist())
    with zipfile.ZipFile(install) as z:
        dlls=[n for n in z.namelist() if n.lower().endswith('.dll')]
        assert sorted(Path(n).name for n in dlls)==['ResourceLedger.Core.dll','ResourceLedger.dll']
        assert 'ResourceLedger/About/About.xml' in z.namelist()
        assert 'ResourceLedger/Textures/UI/Buttons/ResourceLedger.png' in z.namelist()
        assert not any('/src/' in n or '/obj/' in n for n in z.namelist())
    manifest={'version':ver,'experimental':True,'gameplay_tested':False,'files':[{'name':p.name,'bytes':p.stat().st_size,'sha256':sha(p)} for p in [install,source]]}
    bundle=out/f'ResourceLedger_{ver}_bundle.zip'
    with zipfile.ZipFile(bundle,'w',zipfile.ZIP_DEFLATED) as z:
        for p in [install,source]:z.write(p,p.name)
        for name in ['README.md','VALIDATION.md']:z.write(ROOT/name,name)
        z.writestr('START_HERE.md',f'# Resource Ledger {ver}\n\nゲーム用は ResourceLedger_{ver}_Mod.zip です。展開した ResourceLedger フォルダーを RimWorld/Mods に配置し、Harmony 1.7対応版と一緒に有効化してください。\n\nSource.zip は開発用です。ゲーム画面・通常プレイでの実機確認は未実施です。README.md と VALIDATION.md を参照してください。\n')
        z.writestr('MANIFEST.json',json.dumps(manifest,ensure_ascii=False,indent=2))
    with zipfile.ZipFile(bundle) as z:assert z.testzip() is None
    archives=[install,source,bundle]
    (out/'SHA256SUMS.txt').write_text(''.join(sha(p)+'  '+p.name+'\n' for p in archives),encoding='utf-8')
    print(json.dumps({'version':ver,'files':[{'path':str(p),'bytes':p.stat().st_size,'sha256':sha(p)} for p in archives]},indent=2))

if __name__=='__main__':main()
