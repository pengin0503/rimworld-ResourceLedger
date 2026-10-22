from pathlib import Path
import shutil, subprocess, sys, tempfile, unittest

ROOT=Path(__file__).resolve().parents[1]

class BuildManifestTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name)
        (self.root/'tools').mkdir();(self.root/'src').mkdir();(self.root/'bin').mkdir()
        shutil.copy2(ROOT/'tools/build_manifest.py',self.root/'tools/build_manifest.py')
        (self.root/'Directory.Build.props').write_text('<Project><PropertyGroup><Version>0.2.0</Version></PropertyGroup></Project>')
        (self.root/'ResourceLedger.csproj').write_text('<Project/>')
        (self.root/'src/Example.cs').write_text('class Example {}\n')
        for name in ['ResourceLedger.dll','ResourceLedger.Core.dll']:
            (self.root/'bin'/name).write_bytes(b'MZ-test-fixture')
        self.run_helper('stamp')
        self.assertTrue((self.root/'bin/build-info.json').is_file(),'Build did not produce a source manifest')

    def run_helper(self,mode):
        return subprocess.run([sys.executable,str(self.root/'tools/build_manifest.py'),mode,'--assemblies',str(self.root/'bin'),'--info',str(self.root/'bin/build-info.json')],capture_output=True,text=True)

    def test_unchanged_build_is_accepted(self):
        result=self.run_helper('verify');self.assertEqual(0,result.returncode,result.stderr)

    def test_source_change_requires_rebuild(self):
        (self.root/'src/Example.cs').write_text('class Changed {}\n')
        result=self.run_helper('verify');self.assertNotEqual(0,result.returncode)
        self.assertIn('source changed',result.stderr)

    def test_assembly_change_is_rejected(self):
        (self.root/'bin/ResourceLedger.dll').write_bytes(b'MZ-different-build')
        result=self.run_helper('verify');self.assertNotEqual(0,result.returncode)
        self.assertIn('assembly changed',result.stderr)

    def test_line_endings_do_not_invalidate_same_source(self):
        (self.root/'src/Example.cs').write_bytes(b'class Example {}\r\n')
        result=self.run_helper('verify');self.assertEqual(0,result.returncode,result.stderr)

if __name__=='__main__':unittest.main()
