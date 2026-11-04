"""Run the real shell entrypoints with a fake SDK; never execute app/database code."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]

class SourceVerificationTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix='farm verifier ')
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / 'scripts').mkdir()
        for name in ('verify-source.sh', 'cloud-verify.sh'):
            shutil.copy2(ROOT / 'scripts' / name, self.root / 'scripts' / name)
        tools = self.root / 'fake-bin'; tools.mkdir()
        fake = tools / 'dotnet'
        fake.write_text('#!' + sys.executable + '\n' + '''import json,os,pathlib,sys
args=sys.argv[1:]
with open(os.environ['CALL_LOG'],'a') as f: f.write(json.dumps(args)+'\\n')
fail=os.environ.get('FAIL_STEP','').split()
if fail and args[:len(fail)]==fail:
 print('synthetic SDK failure',file=sys.stderr);sys.exit(23)
if '--output' in args:
 target=pathlib.Path(args[args.index('--output')+1])
 if args[0]=='publish': target.mkdir();(target/'app.txt').write_text('synthetic publish')
 else: target.write_text('synthetic migration review')
if '--results-directory' in args:
 target=pathlib.Path(args[args.index('--results-directory')+1]);target.mkdir();(target/'tests.trx').write_text('synthetic test result')
print('synthetic SDK success')
''')
        fake.chmod(0o700)
        self.env = dict(os.environ, PATH=str(tools)+os.pathsep+os.environ['PATH'], CALL_LOG=str(self.root/'calls.jsonl'))

    def run_script(self, entry='cloud-verify.sh', fail='', args=()):
        before = set((self.root/'artifacts/source-verification').glob('*'))
        result = subprocess.run(['bash', str(self.root/'scripts'/entry), *args], cwd=self.root, env=dict(self.env, FAIL_STEP=fail), text=True, capture_output=True)
        after = set((self.root/'artifacts/source-verification').glob('*'))
        return result, after-before

    def test_repeated_entrypoints_preserve_previous_evidence_and_publish(self):
        old = self.root/'evidence'; old.mkdir(); (old/'build.log').write_text('historical')
        previous = self.root/'.cloud/publish'; previous.mkdir(parents=True); (previous/'app.txt').write_text('previous package')
        result, paths = self.run_script(); self.assertEqual(result.returncode, 0, result.stderr)
        first, = paths
        snapshot = {str(p.relative_to(first)):p.read_bytes() for p in first.rglob('*') if p.is_file()}
        result, paths = self.run_script('verify-source.sh'); self.assertEqual(result.returncode, 0, result.stderr)
        second, = paths; self.assertNotEqual(first, second)
        self.assertEqual(snapshot, {str(p.relative_to(first)):p.read_bytes() for p in first.rglob('*') if p.is_file()})
        self.assertEqual((old/'build.log').read_text(), 'historical')
        self.assertEqual((previous/'app.txt').read_text(), 'previous package')
        self.assertTrue((second/'publish/app.txt').exists());self.assertTrue((second/'test-results/tests.trx').exists())
        self.assertIn(str(second), result.stdout)

    def test_failure_at_every_stage_stops_later_steps_and_keeps_stderr(self):
        stages = [('--info','sdk.txt'),('restore','restore.log'),('build','build.log'),('test','test.log'),('tool restore','tool-restore.log'),('ef migrations has-pending-model-changes','model-check.log'),('ef migrations script','migration-script.log'),('publish','publish.log')]
        for index,(stage,log) in enumerate(stages):
            with self.subTest(stage=stage):
                Path(self.env['CALL_LOG']).write_text('')
                result,paths = self.run_script(fail=stage); run, = paths
                self.assertNotEqual(result.returncode,0)
                self.assertNotIn('SOURCE VERIFICATION PASSED',result.stdout)
                self.assertIn('synthetic SDK failure',(run/log).read_text())
                self.assertEqual(index+1,len(Path(self.env['CALL_LOG']).read_text().splitlines()))
                self.assertFalse((run/'publish').exists())

    def test_all_output_paths_stay_in_new_run_and_no_database_update_is_requested(self):
        result, paths = self.run_script();self.assertEqual(result.returncode,0);run, = paths
        calls=[json.loads(line) for line in Path(self.env['CALL_LOG']).read_text().splitlines()]
        self.assertEqual(8,len(calls))
        for args in calls:
            self.assertNotIn('database',args);self.assertNotIn('run',args)
            for key in ('--output','--results-directory'):
                if key in args: self.assertTrue(Path(args[args.index(key)+1]).is_relative_to(run))

    def test_unsupported_arguments_fail_before_any_sdk_command_or_output(self):
        result,paths=self.run_script(args=('--deploy',))
        self.assertEqual(result.returncode,2);self.assertFalse(paths)
        self.assertFalse(Path(self.env['CALL_LOG']).exists())

if __name__ == '__main__':
    unittest.main()
