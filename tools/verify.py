from pathlib import Path
import argparse, re, xml.etree.ElementTree as ET
from build_manifest import version, verify as verify_prebuilt
ROOT=Path(__file__).resolve().parents[1]
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--prebuilt',action='store_true');options=parser.parse_args()
    assert ET.parse(ROOT/'mod/About/About.xml').findtext('modVersion')==version(),'metadata version mismatch'
    icon=(ROOT/'mod/Textures/UI/Buttons/ResourceLedger.png').read_bytes()
    assert icon[:8]==bytes([137,80,78,71,13,10,26,10]) and icon[25] in {4,6},'icon must be a transparent PNG'
    definition=ET.parse(ROOT/'mod/Defs/ResourceLedger.xml').find('MainButtonDef')
    assert definition.findtext('iconPath')=='UI/Buttons/ResourceLedger'
    if options.prebuilt:verify_prebuilt()
    xmls=list((ROOT/'mod').rglob('*.xml'))
    for path in xmls:
        ET.parse(path)
    keyed={}
    for lang in ['English','Japanese']:
        node=ET.parse(ROOT/f'mod/Languages/{lang}/Keyed/ResourceLedger.xml').getroot()
        keyed[lang]={n.tag:n.text or '' for n in node}
        assert len(keyed[lang])==len(list(node)),f'duplicate translation key: {lang}'
    assert keyed['English'].keys()==keyed['Japanese'].keys(),'language keys differ'
    for key in keyed['English']:
        assert set(re.findall(r'\{\d+\}',keyed['English'][key]))==set(re.findall(r'\{\d+\}',keyed['Japanese'][key])),key
    source='\n'.join(p.read_text(encoding='utf-8-sig') for p in (ROOT/'src').rglob('*.cs'))
    assert 'NotImplementedException' not in source,'unfinished implementation'
    assert '\ufffd' not in source,'invalid text encoding'
    for key in re.findall(r'(?:TextCatalog\.Get|\bT)\("([A-Za-z0-9_]+)"(?=\s*[,\)])',source):
        assert 'RL_'+key in keyed['English'],f'missing static translation: {key}'
    models=(ROOT/'src/Core/Models.cs').read_text(encoding='utf-8-sig')
    for enum,prefix in [('Cause','Cause_'),('AlertKind','Alert_')]:
        body=re.search(r'public enum '+enum+r'\s*\{([^}]+)\}',models).group(1)
        for value in body.split(','):
            value=value.strip().split('=')[0].strip()
            assert 'RL_'+prefix+value in keyed['English'],value
    for state in ['Owned','Changed','History']:
        assert 'RL_Visibility_'+state in keyed['English']
    assembly_dir=ROOT/('mod/1.7/Assemblies' if options.prebuilt else 'bin/Release/net10.0')
    for name in ['ResourceLedger.dll','ResourceLedger.Core.dll']:
        data=(assembly_dir/name).read_bytes()
        assert data[:2]==b'MZ' and len(data)>1000,name
    print(f'PASS artifact verification: {len(xmls)} XML files, {len(keyed["English"])} bilingual keys, 2 PE assemblies, transparent icon, no stubs')
if __name__=='__main__':main()

