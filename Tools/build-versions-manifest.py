#!/usr/bin/env python3
"""Build the version manifest published at the documentation site root.

Every documentation page ever published fetches this one file. Those pages are frozen — the
tag they were built from is immutable and the plan forbids republishing — so the manifest is
the only thing that can still tell a reader on an old release that a newer one exists.

It is rebuilt from three sources, because no one of them is complete on its own: the manifest
already published (authoritative for releases the listing may not return), the folders listed
at the site root, and the release being published now.

Run: python3 Tools/build-versions-manifest.py --version 0.2.0 --published-from published.txt
"""
import argparse
import importlib.util
import json
import os
import sys
from functools import cmp_to_key

BUMP_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "bump-version.py")


def load_bump():
    """Semver precedence lives in bump-version.py, which is loaded by path because of the
    hyphen in its name. The same approach its own tests use — one implementation, not two."""
    spec = importlib.util.spec_from_file_location("bump_version", BUMP_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def core(name):
    """The part of a folder name that carries precedence. Build metadata is excluded from it by
    semver, but it is still part of the folder's name, so it is stripped only for comparison."""
    return name.split("+", 1)[0]


def build_manifest(published, version, previous=None):
    bump = load_bump()

    def is_release(name):
        try:
            bump.parse_semver(core(name))
        except bump.BumpError:
            return False
        return True

    bump.parse_semver(core(version))

    names = {name for name in published if is_release(name)}

    # A release already named by the published manifest stays named by it. The site-root
    # listing is cached for a minute and capped in size, so it is not proof a release is gone —
    # and dropping one tells its readers there is nothing newer, which is the failure this
    # whole mechanism exists to prevent.
    if isinstance(previous, dict) and isinstance(previous.get("versions"), list):
        names.update(name for name in previous["versions"] if isinstance(name, str) and is_release(name))

    names.add(version)

    def precedence(a, b):
        return bump.compare_semver(core(a), core(b))

    # Sorted by name first so that names of equal precedence — which is exactly what build
    # metadata produces — come out in the same order on every run rather than in set order.
    versions = sorted(sorted(names), key=cmp_to_key(precedence))

    stable = [name for name in versions if not bump.parse_semver(core(name))[3]]

    manifest = {
        "versions": versions,
        # Informational: each page recomputes what is newest from `versions`, because a page
        # that is already published can never be corrected if this is ever wrong.
        "latestStable": stable[-1] if stable else None,
    }

    # A relocation or retirement notice is added by hand to the published manifest. A routine
    # deploy has nothing to say, but it must not erase what someone deliberately said — a
    # custom-domain migration is exactly the case where a later tag on the old line is likely.
    if isinstance(previous, dict):
        notice = previous.get("notice")
        if isinstance(notice, dict) and isinstance(notice.get("text"), str) and notice["text"]:
            manifest["notice"] = notice

    return manifest


def read_json(path):
    if not path:
        return None
    try:
        with open(path, encoding="utf-8") as handle:
            text = handle.read().strip()
    except OSError:
        return None
    if not text:
        return None
    try:
        return json.loads(text)
    except ValueError:
        # An unreadable published manifest is replaced rather than allowed to stop a release.
        print("warning: the published manifest is not valid JSON and is being rebuilt.", file=sys.stderr)
        return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True, help="the release being published")
    parser.add_argument(
        "--published-from",
        required=True,
        help="file of names already published at the site root, one per line",
    )
    parser.add_argument(
        "--previous-from",
        default=None,
        help="file holding the manifest already published, if there is one",
    )
    args = parser.parse_args()

    with open(args.published_from, encoding="utf-8") as handle:
        published = [name.strip() for name in handle.read().splitlines() if name.strip()]

    bump = load_bump()
    try:
        manifest = build_manifest(published, args.version, read_json(args.previous_from))
    except bump.BumpError as exc:
        print("::error::{0}".format(exc), file=sys.stderr)
        return 1

    json.dump(manifest, sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
