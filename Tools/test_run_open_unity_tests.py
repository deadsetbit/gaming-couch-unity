#!/usr/bin/env python3
"""Contract tests for run-open-unity-tests.py::wait_for_completion.

Locks in the harness exit-code contract that remediation Task 1 depends on: a terminal
status maps to the right exit code, and a run left at "started" times out with exit 2.

Run: python3 Tools/test_run_open_unity_tests.py
"""
import importlib.util
import json
import os
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


if __name__ == "__main__":
    unittest.main()
