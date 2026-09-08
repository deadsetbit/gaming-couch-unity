#!/usr/bin/env python3
import argparse
import glob
import hashlib
import json
import os
import stat
import sys
import time
import uuid


APP_DATA_DIRECTORY_NAME = "Gaming Couch"
BRIDGE_DIRECTORY_NAME = "CodexTestBridge"
SESSION_MANIFEST_NAME = "session.json"
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
    parser.add_argument(
        "--no-refresh",
        action="store_true",
        help=(
            "Skip the Editor-side AssetDatabase.Refresh() the bridge runs before the tests. "
            "By default the bridge refreshes so on-disk script edits recompile without the Editor "
            "being focused; pass this to run against the currently compiled assemblies instead."
        ),
    )
    parser.add_argument("--timeout", type=float, default=300.0, help="Seconds to wait for completion.")
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


def normalize_project_path(path):
    return os.path.abspath(path).rstrip(os.sep)


def path_key(path):
    return os.path.normcase(normalize_project_path(path))


def local_app_data_dir():
    if os.name == "nt":
        return os.environ.get("LOCALAPPDATA") or os.path.expanduser("~/AppData/Local")
    return os.path.expanduser("~/.local/share")


def project_hash(project_path):
    return hashlib.sha256(project_path.encode("utf-8")).hexdigest()[:32]


def bridge_root(project_path):
    return os.path.join(
        bridge_parent_dir(),
        project_hash(project_path),
    )


def bridge_parent_dir():
    return os.path.join(
        local_app_data_dir(),
        APP_DATA_DIRECTORY_NAME,
        BRIDGE_DIRECTORY_NAME,
    )


def bridge_manifest_path(project_path):
    return os.path.join(bridge_root(project_path), SESSION_MANIFEST_NAME)


def bridge_manifest_paths(project_path):
    expected_path = os.path.abspath(bridge_manifest_path(project_path))
    yield expected_path

    pattern = os.path.join(bridge_parent_dir(), "*", SESSION_MANIFEST_NAME)
    for candidate_path in glob.glob(pattern):
        candidate_path = os.path.abspath(candidate_path)
        if candidate_path != expected_path:
            yield candidate_path


def is_inside(path, directory):
    path = os.path.abspath(path)
    directory = os.path.abspath(directory)
    try:
        return os.path.commonpath([path, directory]) == directory
    except ValueError:
        return False


def assert_inside(path, directory, label):
    if not is_inside(path, directory):
        raise RuntimeError(f"{label} must stay inside {directory}: {path}")


def assert_no_symlink(path, label):
    is_junction = hasattr(os.path, "isjunction") and os.path.isjunction(path)
    if os.path.lexists(path) and (os.path.islink(path) or is_junction):
        raise RuntimeError(f"{label} must not be a symlink: {path}")


def assert_no_symlink_tree(root, path, label, include_leaf=True):
    root = os.path.abspath(root)
    path = os.path.abspath(path)
    assert_inside(path, root, label)
    assert_no_symlink(root, label)

    target = path if include_leaf else os.path.dirname(path)
    if target == root:
        return

    relative = os.path.relpath(target, root)
    current = root
    for segment in relative.split(os.sep):
        if segment in ("", "."):
            continue
        current = os.path.join(current, segment)
        assert_no_symlink(current, label)


def ensure_private_directory(path, label):
    if not os.path.isdir(path):
        raise RuntimeError(f"{label} does not exist: {path}")
    assert_no_symlink(path, label)
    if os.name != "nt":
        mode = stat.S_IMODE(os.stat(path).st_mode)
        if mode & 0o077:
            os.chmod(path, mode & ~0o077)


def load_bridge_session(project_path):
    errors = []
    for manifest_path in bridge_manifest_paths(project_path):
        if not os.path.exists(manifest_path):
            continue

        root = os.path.dirname(os.path.abspath(manifest_path))
        try:
            assert_inside(root, bridge_parent_dir(), "Bridge root")
            ensure_private_directory(root, "Bridge root")
            assert_no_symlink_tree(root, manifest_path, "Bridge manifest")
            with open(manifest_path, "r", encoding="utf-8") as manifest_file:
                session = json.load(manifest_file)

            required_keys = ("projectPath", "sessionId", "token", "requestPath", "outputDirectory")
            missing_keys = [key for key in required_keys if not session.get(key)]
            if missing_keys:
                raise RuntimeError(f"Bridge session manifest is missing: {', '.join(missing_keys)}")

            manifest_project_path = normalize_project_path(session["projectPath"])
            if path_key(manifest_project_path) != path_key(project_path):
                errors.append(
                    "Bridge session project path does not match the requested project.\n"
                    f"Manifest project: {manifest_project_path}\n"
                    f"Requested project: {project_path}"
                )
                continue

            session_dir = os.path.dirname(os.path.abspath(session["requestPath"]))
            output_dir = os.path.abspath(session["outputDirectory"])
            assert_inside(session_dir, root, "Bridge request directory")
            assert_inside(output_dir, session_dir, "Bridge output directory")
            ensure_private_directory(session_dir, "Bridge request directory")
            ensure_private_directory(output_dir, "Bridge output directory")
            assert_no_symlink_tree(root, session["requestPath"], "Bridge request path")
            return session
        except (OSError, RuntimeError, json.JSONDecodeError) as error:
            errors.append(str(error))

    expected_path = bridge_manifest_path(project_path)
    message = (
        "No active Unity bridge session manifest was found. "
        f"Open or refresh the Unity Editor for {project_path}, then retry.\n"
        "The bridge is opt-in: the Editor can be open but the bridge disabled. Enable it by "
        "creating the marker file '<project>/.gamingcouch/codex-bridge.enabled' or setting the "
        "GAMINGCOUCH_CODEX_TEST_BRIDGE=1 environment variable before launching the Editor, then "
        "let it domain-reload once (no live watcher).\n"
        f"Expected manifest: {expected_path}"
    )
    if errors:
        message += "\nChecked manifests:\n" + "\n".join(errors)
    raise RuntimeError(message)


def default_output_path(output_dir, request_id, extension):
    return os.path.join(output_dir, f"gaming-couch-unity-test-{request_id}.{extension}")


def write_request(args, session, project_path, request_id):
    assembly_names = [] if args.all_assemblies else (args.assembly or [DEFAULT_ASSEMBLY])
    manifest_request_path = os.path.abspath(session["requestPath"])
    request_path = manifest_request_path
    session_dir = os.path.dirname(os.path.abspath(session["requestPath"]))
    assert_inside(request_path, session_dir, "Request path")
    assert_no_symlink_tree(session_dir, request_path, "Request path")

    request = {
        "requestId": request_id,
        "token": session["token"],
        "projectPath": project_path,
        "testMode": normalize_mode(args.mode),
        "testNames": args.test_names or [],
        "groupNames": args.group_names or [],
        "categoryNames": args.category_names or [],
        "assemblyNames": assembly_names,
        "runSynchronously": bool(args.sync),
        "skipRefresh": bool(args.no_refresh),
    }

    request_dir = os.path.dirname(request_path)
    ensure_private_directory(request_dir, "Request directory")

    temp_path = os.path.join(request_dir, f".{os.path.basename(request_path)}.{request_id}.tmp")
    try:
        flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
        fd = os.open(temp_path, flags, 0o600)
        with os.fdopen(fd, "w", encoding="utf-8") as request_file:
            json.dump(request, request_file, indent=2)
            request_file.write("\n")
        assert_no_symlink_tree(session_dir, request_path, "Request path")
        os.replace(temp_path, request_path)
    finally:
        if os.path.exists(temp_path):
            os.unlink(temp_path)
    return request_path


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

    if last_state is None:
        print(f"timeout: Unity never picked up the request after {timeout:g}s")
        print(
            "hint: is the Editor open on this project and not suspended? On macOS, App Nap can "
            "freeze a backgrounded Editor's poll loop -- see AGENTS.md for the fix."
        )
    elif last_state == "refreshing":
        print(f"timeout: Unity was still refreshing/recompiling after {timeout:g}s")
        print("hint: check the Unity console for compile errors that are blocking the run.")
    else:
        print(f"timeout: no terminal Unity test status (last state: {last_state}) after {timeout:g}s")
    print(f"status: {status_path}")
    return 2


def main():
    args = parse_args()
    project_path = normalize_project_path(args.project_path)
    try:
        session = load_bridge_session(project_path)
    except (OSError, RuntimeError, json.JSONDecodeError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 2

    request_id = uuid.uuid4().hex
    output_dir = os.path.abspath(session["outputDirectory"])
    try:
        status_path = default_output_path(output_dir, request_id, "json")
        assert_no_symlink_tree(output_dir, status_path, "Status path")
        request_path = write_request(args, session, project_path, request_id)
    except (OSError, RuntimeError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 2

    print(f"requested: {normalize_mode(args.mode)} tests for {project_path}")
    print(f"request: {request_path}")
    print(f"status: {status_path}")
    return wait_for_completion(status_path, request_id, args.timeout)


if __name__ == "__main__":
    sys.exit(main())
