#!/usr/bin/env python3
"""Build the version manifest published at the documentation site root.

Every documentation page ever published fetches this one file. Those pages are frozen — the
tag they were built from is immutable and the plan forbids republishing — so the manifest is
the only thing that can still tell a reader on an old version that a newer one exists.

Reads the names already published at the site root, keeps the ones that name a release, adds
the release being published, and writes the result as JSON.

Run: python3 Tools/build-versions-manifest.py --version 0.2.0 --published-from published.txt
"""
import argparse
import importlib.util
import json
import os
import sys
from functools import cmp_to_key

SCHEMA_VERSION = 1

BUMP_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "bump-version.py")


def load_bump():
    """Semver precedence lives in bump-version.py, which is loaded by path because of the
    hyphen in its name. The same approach its own tests use — one implementation, not two."""
    spec = importlib.util.spec_from_file_location("bump_version", BUMP_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def build_manifest(published, version):
    bump = load_bump()

    def is_release(name):
        try:
            # Build metadata is not part of precedence, but it is part of the folder name, so
            # it is validated away and kept.
            bump.parse_semver(name.split("+", 1)[0])
        except bump.BumpError:
            return False
        return True

    names = {name for name in published if is_release(name)}
    names.add(version)

    def precedence(a, b):
        return bump.compare_semver(a.split("+", 1)[0], b.split("+", 1)[0])

    versions = sorted(names, key=cmp_to_key(precedence))

    stable = [name for name in versions if not bump.parse_semver(name.split("+", 1)[0])[3]]

    return {
        "schemaVersion": SCHEMA_VERSION,
        "versions": versions,
        # Informational: each page recomputes what is newest from `versions`, because a page
        # that is already published can never be corrected if this is ever wrong.
        "latestStable": stable[-1] if stable else None,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True, help="the release being published")
    parser.add_argument(
        "--published-from",
        required=True,
        help="file of names already published at the site root, one per line; - for stdin",
    )
    args = parser.parse_args()

    if args.published_from == "-":
        published = sys.stdin.read().splitlines()
    else:
        with open(args.published_from, encoding="utf-8") as handle:
            published = handle.read().splitlines()

    manifest = build_manifest([name.strip() for name in published if name.strip()], args.version)
    json.dump(manifest, sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
