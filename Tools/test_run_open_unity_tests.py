#!/usr/bin/env python3
"""Contract tests for run-open-unity-tests.py::wait_for_completion.

Locks in the harness exit-code contract that remediation Task 1 depends on: a terminal
status maps to the right exit code, and a run left at "started" times out with exit 2.

Run: python3 Tools/test_run_open_unity_tests.py
"""
import argparse
import importlib.util
import json
import os
import shutil
import tempfile
import unittest

HARNESS_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "run-open-unity-tests.py")


def load_harness():
    spec = importlib.util.spec_from_file_location("run_open_unity_tests", HARNESS_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


harness = load_harness()


class WaitForCompletionContractTests(unittest.TestCase):
    def _status_file(self, request_id, state):
        handle = tempfile.NamedTemporaryFile(
            "w", suffix=".json", delete=False, encoding="utf-8"
        )
        json.dump({"requestId": request_id, "state": state}, handle)
        handle.close()
        self.addCleanup(os.unlink, handle.name)
        return handle.name

    def test_completed_returns_zero(self):
        path = self._status_file("req-1", "completed")
        self.assertEqual(harness.wait_for_completion(path, "req-1", timeout=5), 0)

    def test_failed_returns_one(self):
        path = self._status_file("req-2", "failed")
        self.assertEqual(harness.wait_for_completion(path, "req-2", timeout=5), 1)

    def test_error_returns_one(self):
        path = self._status_file("req-3", "error")
        self.assertEqual(harness.wait_for_completion(path, "req-3", timeout=5), 1)

    def test_rejected_returns_one(self):
        path = self._status_file("req-4", "rejected")
        self.assertEqual(harness.wait_for_completion(path, "req-4", timeout=5), 1)

    def test_stuck_started_times_out_with_two(self):
        # The false-timeout the C# fix prevents: a successful sync run whose status is
        # left at "started" (never overwritten with the terminal state) times out.
        path = self._status_file("req-5", "started")
        self.assertEqual(harness.wait_for_completion(path, "req-5", timeout=0.1), 2)

    def test_mismatched_request_id_is_ignored_and_times_out(self):
        path = self._status_file("other-req", "completed")
        self.assertEqual(harness.wait_for_completion(path, "req-6", timeout=0.1), 2)

    def test_refreshing_is_non_terminal_and_times_out(self):
        # The pre-run recompile publishes a "refreshing" status; it must not be treated as terminal,
        # so a run that never advances past it times out (exit 2) rather than reporting success.
        path = self._status_file("req-7", "refreshing")
        self.assertEqual(harness.wait_for_completion(path, "req-7", timeout=0.1), 2)


class WriteRequestSkipRefreshTests(unittest.TestCase):
    """The request payload must carry skipRefresh so the bridge knows whether to recompile first."""

    def _session(self):
        session_dir = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, session_dir, ignore_errors=True)
        os.chmod(session_dir, 0o700)
        output_dir = os.path.join(session_dir, "outputs")
        os.mkdir(output_dir, 0o700)
        return {
            "projectPath": session_dir,
            "sessionId": "s1",
            "token": "t1",
            "requestPath": os.path.join(session_dir, "request.json"),
            "outputDirectory": output_dir,
        }

    def _args(self, no_refresh):
        return argparse.Namespace(
            mode="EditMode",
            assembly=None,
            all_assemblies=False,
            test_names=None,
            group_names=None,
            category_names=None,
            sync=False,
            no_refresh=no_refresh,
        )

    def _written_request(self, no_refresh):
        session = self._session()
        request_id = harness.uuid.uuid4().hex
        request_path = harness.write_request(
            self._args(no_refresh), session, session["projectPath"], request_id
        )
        with open(request_path, encoding="utf-8") as handle:
            return json.load(handle)

    def test_skip_refresh_defaults_false(self):
        data = self._written_request(no_refresh=False)
        self.assertIn("skipRefresh", data)
        self.assertFalse(data["skipRefresh"])

    def test_no_refresh_sets_skip_refresh_true(self):
        data = self._written_request(no_refresh=True)
        self.assertTrue(data["skipRefresh"])


if __name__ == "__main__":
    unittest.main()
