#!/usr/bin/env python3
"""Bump the Gaming Couch Unity package version and (optionally) the DevApp side.

This is the documented, one-command protocol for releasing a new package version.
It keeps public/package/package.json (the single source of name/version) and the
baked public/package/Runtime/Resources/GamingCouchRuntimeInfo.json in lockstep,
verifies them with Tools/check-runtime-package-info.py, commits + tags, and can
hand off to the monorepo's DevApp release helper to register the new unity tag
downstream.

Flow:
  1. Warn (y/N) if not on 'main'.
  2. Semver-bump public/package/package.json (npm-style bump keyword or an explicit X.Y.Z).
  3. Re-bake public/package/Runtime/Resources/GamingCouchRuntimeInfo.json to match.
  4. Verify with Tools/check-runtime-package-info.py (restores files on failure).
  5. Commit 'chore(release): <version>' + create the 'unity-<version>' tag.
  6. Prompt (y/N) to push branch + tag.
  7. Prompt (y/N) to run the monorepo's DevApp release helper
     (devspace/devapp/scripts/prepare-devapp-release.sh) for the latest DevApp.

Usage:
  Tools/bump-version.py <bump> [--preid=<id>] [--dry-run] [--yes]
                        [--monorepo-dir=<path>]

  <bump>            patch | minor | major | prerelease | prepatch | preminor
                   | premajor | X.Y.Z[-pre]
  --preid=<id>      prerelease identifier for pre* bumps: alpha | beta | rc
  --dry-run         print the computed version + planned actions, then stop
  --yes             skip confirmation prompts (non-interactive)
  --monorepo-dir=<path>
                    path to the gaming-couch-client monorepo. Defaults to
                    $GC_MONOREPO_DIR, else ../gamingcouch/client next to this repo.

Examples (from a prerelease X.Y.Z-alpha.N; package.json holds the real current version):
  Tools/bump-version.py prerelease --preid=alpha   -> X.Y.Z-alpha.N+1
  Tools/bump-version.py prerelease --preid=beta    -> X.Y.Z-beta.0
  Tools/bump-version.py patch                      -> X.Y.Z  (finalize the prerelease)
  Tools/bump-version.py 0.2.0-alpha.0              -> exact version
"""

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parents[1]
PACKAGE_DIR = ROOT_DIR / "public" / "package"
PACKAGE_JSON_PATH = PACKAGE_DIR / "package.json"
BAKED_RUNTIME_INFO_PATH = PACKAGE_DIR / "Runtime" / "Resources" / "GamingCouchRuntimeInfo.json"
CHECK_SCRIPT_PATH = ROOT_DIR / "Tools" / "check-runtime-package-info.py"

PLATFORM = "unity"
GAME_PROTOCOL_VERSION = 1
TAG_PREFIX = "unity-"
MAIN_BRANCH = "main"

BUMP_KEYWORDS = {
    "major",
    "minor",
    "patch",
    "premajor",
    "preminor",
    "prepatch",
    "prerelease",
}

SEMVER_RE = re.compile(
    r"^(?P<major>0|[1-9]\d*)\.(?P<minor>0|[1-9]\d*)\.(?P<patch>0|[1-9]\d*)"
    r"(?:-(?P<pre>[0-9A-Za-z.-]+))?$"
)


class BumpError(Exception):
    pass


# --- semver ------------------------------------------------------------------


def parse_semver(value):
    match = SEMVER_RE.match(value.strip())
    if not match:
        raise BumpError("'{0}' is not a valid X.Y.Z[-prerelease] version.".format(value))
    pre = match.group("pre")
    prerelease = _split_prerelease(pre) if pre else []
    return [
        int(match.group("major")),
        int(match.group("minor")),
        int(match.group("patch")),
        prerelease,
    ]


def _split_prerelease(pre):
    parts = []
    for part in pre.split("."):
        if part.isdigit() and (part == "0" or not part.startswith("0")):
            parts.append(int(part))
        else:
            parts.append(part)
    return parts


def format_semver(version):
    major, minor, patch, prerelease = version
    core = "{0}.{1}.{2}".format(major, minor, patch)
    if prerelease:
        return core + "-" + ".".join(str(part) for part in prerelease)
    return core


def _inc_pre(version, identifier):
    # Port of node-semver's inc('pre', identifier) with the default numeric base 0.
    prerelease = version[3]
    if not prerelease:
        prerelease = [0]
    else:
        for i in range(len(prerelease) - 1, -1, -1):
            if isinstance(prerelease[i], int):
                prerelease[i] += 1
                break
        else:
            prerelease.append(0)
    if identifier:
        candidate = [identifier, 0]
        if prerelease and prerelease[0] == identifier:
            if len(prerelease) < 2 or not isinstance(prerelease[1], int):
                prerelease = candidate
        else:
            prerelease = candidate
    version[3] = prerelease


def bump_version(current, release, preid):
    version = parse_semver(current)
    major, minor, patch, prerelease = version

    if release == "premajor":
        version[3] = []
        version[2] = 0
        version[1] = 0
        version[0] = major + 1
        _inc_pre(version, preid)
    elif release == "preminor":
        version[3] = []
        version[2] = 0
        version[1] = minor + 1
        _inc_pre(version, preid)
    elif release == "prepatch":
        version[3] = []
        bump_core(version, "patch")
        _inc_pre(version, preid)
    elif release == "prerelease":
        if not prerelease:
            bump_core(version, "patch")
        _inc_pre(version, preid)
    elif release in ("major", "minor", "patch"):
        bump_core(version, release)
    else:
        raise BumpError("Unknown bump keyword: {0}".format(release))

    return format_semver(version)


def bump_core(version, release):
    major, minor, patch, prerelease = version
    if release == "major":
        if minor != 0 or patch != 0 or not prerelease:
            version[0] = major + 1
        version[1] = 0
        version[2] = 0
        version[3] = []
    elif release == "minor":
        if patch != 0 or not prerelease:
            version[1] = minor + 1
        version[2] = 0
        version[3] = []
    elif release == "patch":
        if not prerelease:
            version[2] = patch + 1
        version[3] = []


def _compare_identifiers(a, b):
    a_num, b_num = isinstance(a, int), isinstance(b, int)
    if a_num and b_num:
        return (a > b) - (a < b)
    if a_num:
        return -1  # numeric identifiers have lower precedence
    if b_num:
        return 1
    return (str(a) > str(b)) - (str(a) < str(b))


def compare_semver(a, b):
    va, vb = parse_semver(a), parse_semver(b)
    for i in range(3):
        if va[i] != vb[i]:
            return (va[i] > vb[i]) - (va[i] < vb[i])
    pa, pb = va[3], vb[3]
    if pa and not pb:
        return -1  # a prerelease is lower than its release
    if not pa and pb:
        return 1
    for x, y in zip(pa, pb):
        result = _compare_identifiers(x, y)
        if result != 0:
            return result
    return (len(pa) > len(pb)) - (len(pa) < len(pb))


def resolve_new_version(current, bump, preid):
    if bump in BUMP_KEYWORDS:
        return bump_version(current, bump, preid)
    # Explicit version.
    parse_semver(bump)  # validate
    return format_semver(parse_semver(bump))


# --- file edits --------------------------------------------------------------


def read_current_version():
    text = PACKAGE_JSON_PATH.read_text(encoding="utf-8")
    match = re.search(r'"version"\s*:\s*"([^"]+)"', text)
    if not match:
        raise BumpError("Could not find a version field in package.json.")
    return match.group(1), text


def write_package_version(text, current, new):
    updated, count = re.subn(
        r'("version"\s*:\s*")' + re.escape(current) + r'(")',
        lambda m: m.group(1) + new + m.group(2),
        text,
        count=1,
    )
    if count != 1:
        raise BumpError("Could not rewrite the version field in package.json.")
    PACKAGE_JSON_PATH.write_text(updated, encoding="utf-8")


def canonical_runtime_info(name, version):
    # Compact, canonical shape matched by Tools/check-runtime-package-info.py.
    return (
        '{{"platform":"{0}","packageName":"{1}",'
        '"packageVersion":"{2}","gameProtocolVersion":{3}}}'
    ).format(PLATFORM, name, version, GAME_PROTOCOL_VERSION)


def bake_runtime_info(name, new_version):
    BAKED_RUNTIME_INFO_PATH.write_text(
        canonical_runtime_info(name, new_version) + "\n", encoding="utf-8"
    )


def read_package_name():
    import json

    with PACKAGE_JSON_PATH.open("r", encoding="utf-8") as handle:
        return json.load(handle)["name"]


# --- shell helpers -----------------------------------------------------------


def git(*args, capture=False):
    result = subprocess.run(
        ["git", "-C", str(ROOT_DIR), *args],
        check=True,
        text=True,
        stdout=subprocess.PIPE if capture else None,
    )
    return result.stdout.strip() if capture else None


def confirm(prompt, assume_yes):
    if assume_yes:
        print("{0} (auto-yes)".format(prompt))
        return True
    try:
        answer = input("{0} (y/N): ".format(prompt)).strip().lower()
    except EOFError:
        return False
    return answer == "y"


def run_check_script():
    result = subprocess.run([sys.executable, str(CHECK_SCRIPT_PATH)])
    return result.returncode == 0


def resolve_monorepo_dir(explicit):
    if explicit:
        return Path(explicit).expanduser().resolve()
    env = os.environ.get("GC_MONOREPO_DIR")
    if env:
        return Path(env).expanduser().resolve()
    return (ROOT_DIR.parent / "gamingcouch" / "client").resolve()


# --- main --------------------------------------------------------------------


def main():
    parser = argparse.ArgumentParser(add_help=True, description=__doc__)
    parser.add_argument("bump", help="bump keyword or an explicit X.Y.Z[-pre] version")
    parser.add_argument("--preid", default="", help="prerelease id for pre* bumps")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--yes", action="store_true", help="skip confirmation prompts")
    parser.add_argument("--monorepo-dir", default="")
    args = parser.parse_args()

    try:
        current, package_text = read_current_version()
        new_version = resolve_new_version(current, args.bump, args.preid)
    except BumpError as exc:
        print("error: {0}".format(exc), file=sys.stderr)
        return 2

    if new_version == current:
        print("error: computed version equals current ({0}); nothing to do.".format(current), file=sys.stderr)
        return 2

    tag_name = TAG_PREFIX + new_version
    print("Package version: {0} -> {1}".format(current, new_version))
    print("Tag:             {0}".format(tag_name))

    if compare_semver(new_version, current) <= 0:
        print("WARNING: {0} is not greater than the current {1}.".format(new_version, current))
        if not args.dry_run and not confirm("Continue with a non-increasing version?", args.yes):
            print("Aborted.")
            return 1

    # --- branch guard ---
    branch = git("rev-parse", "--abbrev-ref", "HEAD", capture=True)
    if branch != MAIN_BRANCH:
        print("WARNING: you are on '{0}', not '{1}'.".format(branch, MAIN_BRANCH))
        if not args.dry_run and not confirm("Continue the bump on this branch?", args.yes):
            print("Aborted.")
            return 1

    if args.dry_run:
        print("\nDRY RUN — would edit package.json + baked runtime info, then commit "
              "'chore(release): {0}' and tag {1}. No changes made.".format(new_version, tag_name))
        return 0

    if git("tag", "--list", tag_name, capture=True):
        print("error: tag {0} already exists. Published tags are immutable — the mirror "
              "publishes one orphan snapshot per tag and would reject a moved tag as a "
              "non-fast-forward, leaving the public repo serving the old commit. Cut the next "
              "version instead.".format(tag_name), file=sys.stderr)
        return 2

    if not confirm("Apply the bump to {0}?".format(new_version), args.yes):
        print("Aborted.")
        return 1

    # --- edit files ---
    original_baked = BAKED_RUNTIME_INFO_PATH.read_text(encoding="utf-8")
    name = read_package_name()

    def restore():
        PACKAGE_JSON_PATH.write_text(package_text, encoding="utf-8")
        BAKED_RUNTIME_INFO_PATH.write_text(original_baked, encoding="utf-8")

    try:
        write_package_version(package_text, current, new_version)
        bake_runtime_info(name, new_version)
    except BumpError as exc:
        restore()
        print("error: {0} Files restored.".format(exc), file=sys.stderr)
        return 1

    # --- hard check: the invariant this tool owns (baked JSON == package.json) ---
    baked_now = BAKED_RUNTIME_INFO_PATH.read_text(encoding="utf-8").rstrip("\r\n")
    if baked_now != canonical_runtime_info(name, new_version):
        restore()
        print("error: baked runtime info did not match package.json after edit. Files restored.", file=sys.stderr)
        return 1

    # --- advisory: full identity guard (may flag pre-existing, unrelated drift) ---
    if not run_check_script():
        print("\nThe full runtime-identity guard reported issues above.")
        print("The version/baked-JSON part is already verified; failures here may be pre-existing")
        print("and unrelated to the version bump.")
        if not confirm("Continue with commit + tag anyway?", args.yes):
            restore()
            print("Aborted. Files restored.")
            return 1

    # --- commit + tag ---
    # Commit only these two paths (pathspec) so any other staged work is left untouched.
    package_rel = str(PACKAGE_JSON_PATH.relative_to(ROOT_DIR))
    baked_rel = str(BAKED_RUNTIME_INFO_PATH.relative_to(ROOT_DIR))
    git("commit", "-m", "chore(release): {0}".format(new_version), "--", package_rel, baked_rel)
    git("tag", tag_name)
    print("Committed and tagged {0}.".format(tag_name))

    # --- push ---
    manual_push = "git push origin {0} && git push origin {1}".format(branch, tag_name)
    if confirm("Push branch '{0}' and tag {1} to origin?".format(branch, tag_name), args.yes):
        try:
            git("push", "origin", branch)
            git("push", "origin", tag_name)
            print("Pushed.")
        except subprocess.CalledProcessError:
            print("Push failed. The commit + tag are local only. Later: " + manual_push, file=sys.stderr)
    else:
        print("Skipped push. Later: " + manual_push)

    # --- cross-repo: DevApp release helper ---
    if confirm("Also run the monorepo's DevApp release helper for the latest DevApp?", args.yes):
        run_devapp_release(tag_name, args)

    return 0


def run_devapp_release(unity_tag, args):
    monorepo = resolve_monorepo_dir(args.monorepo_dir)
    devapp_dir = monorepo / "devspace" / "devapp"
    script = devapp_dir / "scripts" / "prepare-devapp-release.sh"
    if not script.exists():
        print("Could not find the DevApp release helper at:\n  {0}".format(script), file=sys.stderr)
        print("Set GC_MONOREPO_DIR or pass --monorepo-dir=<path to gaming-couch-client>.", file=sys.stderr)
        return

    print("\nHanding off to the DevApp release helper in:\n  {0}".format(devapp_dir))
    print("When it prompts 'Enter the Unity tag this release should install',")
    print("  enter:  {0}".format(unity_tag))
    try:
        bump = input("DevApp bump [prerelease]: ").strip() or "prerelease"
        preid = input("DevApp --preid (empty for none): ").strip()
    except EOFError:
        print("Aborted DevApp handoff (no input).")
        return

    cmd = ["bash", str(script), bump]
    if preid:
        cmd.append("--preid={0}".format(preid))
    print("Running: {0}  (cwd: {1})".format(" ".join(cmd), devapp_dir))
    subprocess.run(cmd, cwd=str(devapp_dir))


if __name__ == "__main__":
    sys.exit(main())
