from pathlib import Path
import argparse, hashlib, json, xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1]
INFO=ROOT/'mod/About/build-info.json'

def version():
    return ET.parse(ROOT/'Directory.Build.props').findtext('.//Version')

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def source_digest():
    h=hashlib.sha256()
    paths=[ROOT/'Directory.Build.props',ROOT/'ResourceLedger.csproj',*sorted((ROOT/'src').rglob('*.cs')),*sorted((ROOT/'src').rglob('*.csproj'))]
    paths=[p for p in paths if not any(part in {'bin','obj'} for part in p.relative_to(ROOT).parts)]
    for p in paths:
        h.update(p.relative_to(ROOT).as_posix().encode()+b'\0')
        h.update(p.read_text(encoding='utf-8-sig').replace('\r\n','\n').encode()+b'\0')
    return h.hexdigest()

def write(assembly_dir, info_path):
    files={p.name:digest(p) for p in sorted(assembly_dir.glob('*.dll'))}
    info_path.write_text(json.dumps({'version':version(),'source_sha256':source_digest(),'assemblies':files,'build':'RimWorld 1.7 reference build; game binaries are not redistributed.'},indent=2)+'\n',encoding='utf-8')

def verify(assembly_dir=None, info_path=None):
    assembly_dir=assembly_dir or ROOT/'mod/1.7/Assemblies';info_path=info_path or INFO
    info=json.loads(info_path.read_text(encoding='utf-8'))
    assert info['version']==version(),'prebuilt version mismatch'
    assert info['source_sha256']==source_digest(),'source changed after the assemblies were built; rebuild first'
    assert set(info['assemblies'])=={'ResourceLedger.dll','ResourceLedger.Core.dll'}
    for name,sha in info['assemblies'].items():
        assert digest(assembly_dir/name)==sha,f'prebuilt assembly changed: {name}'

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('mode',choices=['stamp','verify'])
    parser.add_argument('--assemblies',type=Path,required=True)
    parser.add_argument('--info',type=Path,required=True)
    args=parser.parse_args()
    if args.mode=='stamp':write(args.assemblies,args.info)
    else:verify(args.assemblies,args.info)
