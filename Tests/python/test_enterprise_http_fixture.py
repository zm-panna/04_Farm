"""Regression checks for evidence preservation and fail-closed HTTP verification."""
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts'))
import enterprise_http_fixture as fixture


class FixtureEvidenceTests(unittest.TestCase):
    def setUp(self):
        scratch = Path(__file__).resolve().parents[2] / 'artifacts'
        scratch.mkdir(exist_ok=True)
        self.tmp = tempfile.TemporaryDirectory(prefix='farm-fixture-regression-', dir=scratch)
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.root_patch = patch.object(fixture, 'ROOT', self.root)
        self.root_patch.start()
        self.addCleanup(self.root_patch.stop)
        self.hash_patch = patch.object(fixture._core, 'digest', return_value='unchanged')
        self.digest = self.hash_patch.start()
        self.addCleanup(self.hash_patch.stop)

    def test_runs_are_distinct_and_historical_evidence_is_untouched(self):
        historic = self.root / 'evidence/http-enterprise-crm/results.json'
        historic.parent.mkdir(parents=True)
        historic.write_text('historical result', encoding='utf-8')
        first, second = fixture.Fixture('crm'), fixture.Fixture('crm')
        self.assertNotEqual(first.out, second.out)
        first.__exit__(None, None, None)
        second.__exit__(None, None, None)
        self.assertEqual(historic.read_text(encoding='utf-8'), 'historical result')
        self.assertEqual(json.loads((first.out / 'results.json').read_text())['status'], 'passed')

    def test_password_leak_fails_command_after_report_and_cleanup(self):
        run = fixture.Fixture('leak')
        run.password = 'synthetic-regression-only'
        (run.out / 'seed.log').write_text(run.password)
        cleanup = run.tmp = Mock()
        with self.assertRaisesRegex(AssertionError, 'HTTP fixture verification failed'):
            run.__exit__(None, None, None)
        self.assertEqual(json.loads((run.out / 'results.json').read_text())['status'], 'failed')
        cleanup.cleanup.assert_called_once()
        self.assertIsNone(run.password)

    def test_changed_runtime_fails_command(self):
        run = fixture.Fixture('binary')
        self.digest.return_value = 'changed'
        with self.assertRaises(AssertionError):
            run.__exit__(None, None, None)
        report = json.loads((run.out / 'results.json').read_text())
        self.assertEqual(report['failed'], 2)

    def test_original_workflow_error_is_not_masked(self):
        run = fixture.Fixture('original')
        error = ValueError('workflow failed')
        self.digest.return_value = 'changed'
        self.assertIsNone(run.__exit__(ValueError, error, None))
        report = json.loads((run.out / 'results.json').read_text())
        self.assertEqual(report['status'], 'failed')
        self.assertEqual(report['error'], str(error))

    def test_query_releases_windows_database_file_handle(self):
        run = fixture.Fixture('query')
        run.dbpath = self.root / 'temporary.db'
        self.assertEqual(run.query('SELECT 42'), [(42,)])
        # Windows rejects deletion while a SQLite connection still owns it.
        run.dbpath.unlink()
        self.assertFalse(run.dbpath.exists())

    def test_cleanup_failure_cannot_leave_a_passing_report(self):
        run = fixture.Fixture('cleanup')
        run.tmp = Mock()
        run.tmp.cleanup.side_effect = PermissionError('synthetic locked file')
        with self.assertRaises(AssertionError):
            run.__exit__(None, None, None)
        report = json.loads((run.out / 'results.json').read_text())
        self.assertEqual(report['status'], 'failed')
        self.assertEqual(report['cleanup'], 'failed')

    def test_fixture_setup_mutations_still_commit_before_connection_closes(self):
        run = fixture.Fixture('transaction')
        run.dbpath = self.root / 'transaction.db'
        run.query('CREATE TABLE sample (id INTEGER PRIMARY KEY, value TEXT)')
        run.query('INSERT INTO sample VALUES (?, ?)', (1, 'synthetic'))
        self.assertEqual(run.query('SELECT value FROM sample WHERE id=1'), [('synthetic',)])
        run.dbpath.unlink()


if __name__ == '__main__':
    unittest.main()
