#!/usr/bin/env python3
"""Refuse to publish a package folder that is not self-contained.

Usage: check-dist-complete.py <package-dir> <tag>

The publish boundary is a folder: everything under it ships, everything outside it cannot.
That makes the residual risks the ones a folder boundary cannot catch by itself — a file lost
in a move, a reference pointing outside the folder, or a secret landing inside it. This gate
refuses the publish in those cases. It needs no Unity licence, so it can gate every release.

Exit 0 pass, non-zero fail, every failure printed as a path plus a reason.

Run: python3 Tools/check-dist-complete.py public/package unity-0.1.0-alpha.9
"""

import json
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

EXPECTED_PACKAGE_NAME = "com.dsb.gamingcouch"
TAG_PREFIX = "unity-"

# Assemblies that legitimately live outside the package. Every by-name reference must resolve
# to an assembly inside the folder or appear here, and the gate prints this list whenever it
# is consulted so that growth is visible in the publish log rather than silent.
EXTERNAL_ASSEMBLY_ALLOWLIST = {
    "Unity.Newtonsoft.Json": "Unity's Newtonsoft JSON package, a declared package dependency",
    "UnityEngine.UI": "uGUI, referenced by the editor assembly behind a version define",
    "Unity.Netcode.Runtime": "NGO, referenced by an assembly gated behind a define constraint",
}

# What a complete package is. Meta pairing only catches half of a loss — the asset or its meta —
# so a move that drops a whole folder, metas included, leaves nothing behind to be unpaired and
# nothing left referencing it. Only naming the parts can catch that.
REQUIRED_FILES = ["package.json", "README.md", "CHANGELOG.md", "LICENSE.md"]
REQUIRED_DIRECTORIES = [
    "Runtime",
    "Editor",
    "Plugins",
    "Tests",
    "Documentation~",
    "ContractFixtures/LocalPlay",
]

# The shipped tests reach the Contract Fixtures by string path, so no assembly-reference check
# can see them and they need an assertion of their own. Each case is a directory holding the
# project file the fixture tests replay.
CONTRACT_FIXTURES_DIR = "ContractFixtures/LocalPlay"
CONTRACT_FIXTURE_CASE_FILE = "gc.dev.json"

# Untracked on developer machines and never present in a CI checkout. Ignored defensively so a
# local run agrees with the CI run; it is not why the meta rule needs an exception.
IGNORED_FILENAMES = {".DS_Store"}

SECRET_SCANNER = "gitleaks"

# `gitleaks dir` — a directory scan rather than the default history scan — exists from 8.19.0.
# An older binary rejects the subcommand and exits 1, which the caller would otherwise have to
# tell apart from a real finding.
MINIMUM_SCANNER_VERSION = (8, 19, 0)

# A findings exit code distinct from 1, which gitleaks also uses for its own errors. Without
# this split a broken invocation reads as a leak and triggers a needless credential rotation.
SECRET_SCAN_LEAK_EXIT_CODE = 2

# Both of gitleaks' in-tree suppression channels are files a leaked commit could add to the
# very folder being scanned. A config there replaces the entire ruleset, so the scan passes
# while detecting nothing. The published folder carries no ignore-list by design, so the gate
# refuses these outright rather than trying to scan around them.
# Compared case-folded: on a case-insensitive filesystem `.GITLEAKS.TOML` is the same file to
# the scanner and a different string to us.
SCANNER_CONFIG_FILENAMES = {".gitleaks.toml", "gitleaks.toml", ".gitleaksignore"}

GUID_REFERENCE_RE = re.compile(r"^GUID:([0-9a-fA-F]{32})$")
META_GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)
VERSION_RE = re.compile(r"(\d+)\.(\d+)\.(\d+)")


def is_hidden_from_unity(relative_path):
    """Unity hides any path with a `~` segment and generates no meta for it.

    That is the only legitimate meta-pairing exception. The package's top-level folders do
    carry committed metas, and those are exactly what a bad `git mv` loses, so exempting them
    would blind the gate to the failure it exists to catch.
    """
    return any(segment.endswith("~") for segment in relative_path.parts)


def iter_package_paths(package_dir):
    for path in sorted(package_dir.rglob("*")):
        relative = path.relative_to(package_dir)
        if is_hidden_from_unity(relative):
            continue
        if path.name in IGNORED_FILENAMES:
            continue
        yield path, relative


def check_manifest(package_dir, tag):
    failures = []
    manifest_path = package_dir / "package.json"

    if not manifest_path.is_file():
        return ["package.json: not found in the package folder"]

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        return ["package.json: not valid JSON ({0})".format(exc)]

    if not isinstance(manifest, dict):
        return ["package.json: valid JSON but not an object, so it declares no package"]

    name = manifest.get("name")
    if name != EXPECTED_PACKAGE_NAME:
        failures.append(
            "package.json: name is {0!r}, expected {1!r}".format(name, EXPECTED_PACKAGE_NAME)
        )

    if not tag.startswith(TAG_PREFIX):
        failures.append(
            "{0}: tag does not start with {1!r}, so it names no package version".format(
                tag, TAG_PREFIX
            )
        )
        return failures

    expected_version = tag[len(TAG_PREFIX) :]
    version = manifest.get("version")
    if version != expected_version:
        failures.append(
            "package.json: version is {0!r}, but tag {1} publishes {2!r}".format(
                version, tag, expected_version
            )
        )

    return failures


def check_required_contents(package_dir):
    failures = []
    for relative in REQUIRED_FILES:
        path = package_dir / relative
        if not path.is_file():
            failures.append("{0}: required file is missing".format(relative))
        elif path.stat().st_size == 0:
            failures.append("{0}: required file is empty".format(relative))
    for relative in REQUIRED_DIRECTORIES:
        path = package_dir / relative
        if not path.is_dir():
            failures.append("{0}: required directory is missing".format(relative))
        elif not any(path.iterdir()):
            failures.append("{0}: required directory is empty".format(relative))
    return failures


def check_no_symlinks(package_dir):
    """A symlink reads as an ordinary file to the walk while its content lives elsewhere, so it
    slips meta pairing, the scanner config check and — unless told otherwise — the secret scan.
    Nothing in a published Unity package needs one."""
    failures = []
    for path in sorted(package_dir.rglob("*")):
        if path.is_symlink():
            failures.append(
                "{0}: symlink in the published folder; its target is not part of the "
                "package".format(path.relative_to(package_dir))
            )
    return failures


def check_meta_pairing(package_dir):
    failures = []
    present = {relative for _, relative in iter_package_paths(package_dir)}

    for _, relative in iter_package_paths(package_dir):
        if relative.suffix == ".meta":
            if relative.with_suffix("") not in present:
                failures.append("{0}: orphan meta, no asset beside it".format(relative))
        elif relative.with_name(relative.name + ".meta") not in present:
            failures.append("{0}: no .meta beside it".format(relative))

    return failures


def read_asmdefs(package_dir):
    """Every .asmdef in the folder, as (relative path, parsed body or None, meta guid)."""
    entries = []
    for path, relative in iter_package_paths(package_dir):
        if path.is_file() and relative.suffix == ".asmdef":
            try:
                body = json.loads(path.read_text(encoding="utf-8"))
                if not isinstance(body, dict):
                    body = None
            except json.JSONDecodeError:
                body = None
            meta_path = path.with_name(path.name + ".meta")
            guid = None
            if meta_path.is_file():
                match = META_GUID_RE.search(meta_path.read_text(encoding="utf-8"))
                if match:
                    guid = match.group(1).lower()
            entries.append((relative, body, guid))
    return entries


def review_asmdef_references(package_dir):
    """Failures, plus whether any by-name reference was weighed against the allowlist."""
    entries = read_asmdefs(package_dir)
    internal_guids = {guid for _, _, guid in entries if guid}
    internal_names = {
        body["name"] for _, body, _ in entries if isinstance(body, dict) and body.get("name")
    }

    failures = []
    allowlist_consulted = False

    for relative, body, _ in entries:
        if body is None:
            failures.append("{0}: not a valid assembly definition object".format(relative))
            continue
        for reference in body.get("references", []) or []:
            guid_match = GUID_REFERENCE_RE.match(str(reference))
            if guid_match:
                if guid_match.group(1).lower() not in internal_guids:
                    failures.append(
                        "{0}: reference {1} does not resolve to an .asmdef inside the "
                        "package".format(relative, reference)
                    )
                continue
            if reference in internal_names:
                continue
            allowlist_consulted = True
            if reference in EXTERNAL_ASSEMBLY_ALLOWLIST:
                continue
            failures.append(
                "{0}: reference {1!r} resolves outside the package and is not on the "
                "external allowlist".format(relative, reference)
            )

    return failures, allowlist_consulted


def format_external_allowlist():
    lines = ["External assembly allowlist ({0} entries):".format(len(EXTERNAL_ASSEMBLY_ALLOWLIST))]
    for name, reason in sorted(EXTERNAL_ASSEMBLY_ALLOWLIST.items()):
        lines.append("- {0} — {1}".format(name, reason))
    return "\n".join(lines)


def check_contract_fixtures(package_dir):
    fixtures = package_dir / CONTRACT_FIXTURES_DIR
    if not fixtures.is_dir():
        return [
            "{0}: missing, but the shipped tests resolve it by path".format(CONTRACT_FIXTURES_DIR)
        ]
    cases = [child for child in fixtures.iterdir() if child.suffix != ".meta"]
    if not cases:
        return [
            "{0}: empty, so the shipped Contract Fixture tests have nothing to "
            "replay".format(CONTRACT_FIXTURES_DIR)
        ]

    failures = []
    for case in sorted(cases):
        relative = "{0}/{1}".format(CONTRACT_FIXTURES_DIR, case.name)
        if not case.is_dir():
            failures.append("{0}: not a fixture case directory".format(relative))
        elif not (case / CONTRACT_FIXTURE_CASE_FILE).is_file():
            failures.append(
                "{0}: fixture case has no {1}, so the test replaying it cannot "
                "run".format(relative, CONTRACT_FIXTURE_CASE_FILE)
            )
    return failures


def check_no_scanner_config_inside(package_dir):
    failures = []
    for _, relative in iter_package_paths(package_dir):
        if relative.name.lower() in SCANNER_CONFIG_FILENAMES:
            failures.append(
                "{0}: secret-scanner configuration inside the published folder would "
                "suppress the scan of that same folder".format(relative)
            )
    return failures


def read_scanner_version(scanner):
    result = subprocess.run([scanner, "version"], capture_output=True, text=True)
    if result.returncode != 0:
        return None
    match = VERSION_RE.search(result.stdout)
    if not match:
        return None
    return tuple(int(part) for part in match.groups())


def build_secret_scan_command(package_dir, scanner=SECRET_SCANNER, ignore_path=None):
    """Directory mode: gitleaks otherwise scans git history, and a published tree has none.

    The ignore path is deliberately somewhere other than the package: pointed at the scanned
    folder it would honour a `.gitleaksignore` sitting inside it, which is the suppression this
    is meant to prevent. `--ignore-gitleaks-allow` closes the comment form, and
    `--follow-symlinks` stops a link standing in for a file whose content is never read.
    """
    return [
        scanner,
        "dir",
        str(package_dir),
        "--no-banner",
        "--redact",
        "--exit-code",
        str(SECRET_SCAN_LEAK_EXIT_CODE),
        "--ignore-gitleaks-allow",
        "--follow-symlinks",
        "--gitleaks-ignore-path",
        str(ignore_path if ignore_path is not None else Path(tempfile.gettempdir())),
    ]


def scan_for_secrets(package_dir, scanner=SECRET_SCANNER):
    """The leak backstop. The Action form of gitleaks needs a paid licence for an
    organization-owned repository, so this drives the binary directly."""
    if shutil.which(scanner) is None:
        return [
            "{0}: secret scanner not found on PATH, so the folder was never scanned".format(
                scanner
            )
        ]

    version = read_scanner_version(scanner)
    if version is None:
        return ["{0}: could not read a version from the scanner".format(scanner)]
    if version < MINIMUM_SCANNER_VERSION:
        return [
            "{0}: version {1} is older than the required {2}, which is where the directory "
            "scan used here was introduced".format(
                scanner,
                ".".join(str(part) for part in version),
                ".".join(str(part) for part in MINIMUM_SCANNER_VERSION),
            )
        ]

    with tempfile.TemporaryDirectory() as ignore_dir:
        result = subprocess.run(
            build_secret_scan_command(package_dir, scanner, ignore_dir),
            capture_output=True,
            text=True,
        )
    detail = (result.stdout + result.stderr).strip()

    if result.returncode == 0:
        return []
    if result.returncode == SECRET_SCAN_LEAK_EXIT_CODE:
        return [
            "{0}: secret scan found something. Treat every hit as leaked and rotate it.\n"
            "{1}".format(package_dir, detail)
        ]
    return [
        "{0}: secret scanner failed to run (exit {1}), so the folder was never "
        "scanned.\n{2}".format(package_dir, result.returncode, detail)
    ]


def run_checks(package_dir, tag):
    package_dir = Path(package_dir)
    if not package_dir.is_dir():
        return ["{0}: not a directory".format(package_dir)], False

    asmdef_failures, allowlist_consulted = review_asmdef_references(package_dir)

    failures = []
    failures.extend(check_manifest(package_dir, tag))
    failures.extend(check_required_contents(package_dir))
    failures.extend(check_no_symlinks(package_dir))
    failures.extend(check_meta_pairing(package_dir))
    failures.extend(asmdef_failures)
    failures.extend(check_contract_fixtures(package_dir))
    failures.extend(check_no_scanner_config_inside(package_dir))
    return failures, allowlist_consulted


def main(argv):
    if len(argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2

    package_dir = Path(argv[1]).resolve()
    tag = argv[2]

    failures, allowlist_consulted = run_checks(package_dir, tag)
    failures.extend(scan_for_secrets(package_dir))

    if allowlist_consulted:
        print(format_external_allowlist())
        print("")

    if failures:
        print("Publish completeness gate FAILED for {0} at {1}.".format(package_dir, tag), file=sys.stderr)
        for failure in failures:
            print("- {0}".format(failure), file=sys.stderr)
        print("", file=sys.stderr)
        print("Nothing is published. Fix the package folder and rerun:", file=sys.stderr)
        print("python3 Tools/check-dist-complete.py {0} {1}".format(argv[1], tag), file=sys.stderr)
        return 1

    print("Publish completeness gate passed.")
    print("- package: {0}".format(package_dir))
    print("- tag: {0}".format(tag))
    print("- contents, meta pairing, assembly references, Contract Fixtures, secret scan: clean")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
