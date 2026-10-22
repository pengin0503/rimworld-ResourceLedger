from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]

class InstallDocumentationTests(unittest.TestCase):
    def test_install_zip_contains_linked_documentation(self):
        with tempfile.TemporaryDirectory() as temporary:
            subprocess.run(
                [sys.executable, str(ROOT / 'tools/package.py'), '--prebuilt', '--output', temporary],
                cwd=ROOT, check=True, capture_output=True, text=True,
            )
            archive, = Path(temporary).glob('*_Mod.zip')
            with zipfile.ZipFile(archive) as package:
                for name in ['README.md', 'VALIDATION.md', 'REVIEW.md', 'LICENSE']:
                    self.assertIn('ResourceLedger/' + name, package.namelist())

if __name__ == '__main__':
    unittest.main()
