#!/usr/bin/env python3
import argparse
import json
import os
import sys
import time
import uuid


REQUEST_PATH = "/tmp/gaming-couch-unity-codex-test-request.json"
DEFAULT_ASSEMBLY = "GamingCouch.Editor.Tests"


def parse_args():
    parser = argparse.ArgumentParser(
        description="Request a Unity Test Runner run from the already-open Unity Editor."
    )
    parser.add_argument("project_path", help="Unity project path for the open Editor to target.")
    parser.add_argument(
        "--mode",
        default="EditMode",
        choices=["EditMode", "editmode", "edit", "PlayMode", "playmode", "play", "All", "all"],
        help="Test mode to run. Default: EditMode.",
    )
    parser.add_argument(
        "--assembly",
        action="append",
        help=(
            "Assembly name to include. Repeat for multiple assemblies. "
            f"Default: {DEFAULT_ASSEMBLY}."
        ),
    )
    parser.add_argument(
        "--all-assemblies",
        action="store_true",
        help="Do not send an assembly filter.",
    )
    parser.add_argument("--test", action="append", dest="test_names", help="Full test name to run.")
    parser.add_argument(
        "--filter",
        action="append",
        dest="group_names",
        help="Regex/full-name group filter to pass to Unity Test Runner.",
    )
    parser.add_argument("--category", action="append", dest="category_names", help="Test category to run.")
    parser.add_argument("--sync", action="store_true", help="Run synchronously for EditMode tests.")
    parser.add_argument("--timeout", type=float, default=300.0, help="Seconds to wait for completion.")
    parser.add_argument("--request-path", default=REQUEST_PATH, help="Bridge request JSON path.")
    parser.add_argument("--status-path", help="Status JSON path.")
    parser.add_argument("--results-path", help="NUnit XML results path.")
    return parser.parse_args()


def normalize_mode(mode):
    value = mode.lower()
    if value in ("edit", "editmode"):
        return "EditMode"
    if value in ("play", "playmode"):
        return "PlayMode"
    if value == "all":
        return "All"
    return mode


def default_output_path(request_id, extension):
    return os.path.join("/tmp", f"gaming-couch-unity-test-{request_id}.{extension}")


def write_request(args, request_id, status_path, results_path):
    assembly_names = [] if args.all_assemblies else (args.assembly or [DEFAULT_ASSEMBLY])
    request = {
        "requestId": request_id,
        "projectPath": os.path.abspath(args.project_path),
        "testMode": normalize_mode(args.mode),
        "testNames": args.test_names or [],
        "groupNames": args.group_names or [],
        "categoryNames": args.category_names or [],
        "assemblyNames": assembly_names,
        "resultsPath": results_path,
        "statusPath": status_path,
        "runSynchronously": bool(args.sync),
    }

    request_dir = os.path.dirname(args.request_path)
    if request_dir:
        os.makedirs(request_dir, exist_ok=True)

    temp_path = f"{args.request_path}.{request_id}.tmp"
    with open(temp_path, "w", encoding="utf-8") as request_file:
        json.dump(request, request_file, indent=2)
        request_file.write("\n")
    os.replace(temp_path, args.request_path)


def load_status(status_path):
    try:
        with open(status_path, "r", encoding="utf-8") as status_file:
            return json.load(status_file)
    except (FileNotFoundError, json.JSONDecodeError):
        return None


def print_summary(status):
    state = status.get("state", "unknown")
    message = status.get("message", "")
    print(f"{state}: {message}")

    counts = (
        status.get("passCount", 0),
        status.get("failCount", 0),
        status.get("skipCount", 0),
        status.get("inconclusiveCount", 0),
    )
    if any(counts):
        print(
            "counts: "
            f"passed={counts[0]} failed={counts[1]} skipped={counts[2]} "
            f"inconclusive={counts[3]}"
        )

    if status.get("resultsPath"):
        print(f"results: {status['resultsPath']}")

    failures = status.get("failures") or []
    for failure in failures[:10]:
        name = failure.get("fullName") or "<unknown test>"
        failure_message = failure.get("message") or ""
        print(f"failed: {name}")
        if failure_message:
            print(f"  {failure_message}")

    if len(failures) > 10:
        print(f"... {len(failures) - 10} more failures omitted")


def wait_for_completion(status_path, request_id, timeout):
    deadline = time.monotonic() + timeout
    last_state = None

    while time.monotonic() < deadline:
        status = load_status(status_path)
        if status and status.get("requestId") == request_id:
            state = status.get("state")
            if state != last_state:
                print_summary(status)
                last_state = state

            if state in ("completed", "failed", "error", "rejected"):
                return 0 if state == "completed" else 1

        time.sleep(0.5)

    print(f"timeout: no completed Unity test status after {timeout:g}s")
    print(f"status: {status_path}")
    return 2


def main():
    args = parse_args()
    request_id = uuid.uuid4().hex
    status_path = args.status_path or default_output_path(request_id, "json")
    results_path = args.results_path or default_output_path(request_id, "xml")

    write_request(args, request_id, status_path, results_path)
    print(f"requested: {normalize_mode(args.mode)} tests for {os.path.abspath(args.project_path)}")
    print(f"request: {args.request_path}")
    print(f"status: {status_path}")
    return wait_for_completion(status_path, request_id, args.timeout)


if __name__ == "__main__":
    sys.exit(main())
