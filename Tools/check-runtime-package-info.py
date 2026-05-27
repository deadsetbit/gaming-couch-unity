#!/usr/bin/env python3
"""Check that package.json is the only package name/version source."""

import json
import re
import sys
from pathlib import Path


ROOT_DIR = Path(__file__).resolve().parents[1]
PACKAGE_JSON_PATH = ROOT_DIR / "package.json"
RUNTIME_INFO_PATH = ROOT_DIR / "Runtime" / "GCRuntimeInfo.cs"
RUNTIME_DIR = ROOT_DIR / "Runtime"
PACKAGE_CODE_DIRS = [
    ROOT_DIR / "Runtime",
    ROOT_DIR / "Editor",
    ROOT_DIR / "Plugins",
]
PACKAGE_CODE_SUFFIXES = {".cs", ".jslib"}
WEBGL_BRIDGE_PATH = ROOT_DIR / "Plugins" / "GamingCouch.jslib"


class CheckError(Exception):
    pass


def read_package_manifest(path):
    try:
        with path.open("r", encoding="utf-8") as package_file:
            manifest = json.load(package_file)
    except FileNotFoundError:
        raise CheckError("package.json was not found.")
    except json.JSONDecodeError as exc:
        raise CheckError("package.json is not valid JSON: {0}".format(exc))

    name = manifest.get("name")
    version = manifest.get("version")

    if not isinstance(name, str) or not name:
        raise CheckError("package.json is missing a non-empty string field: name")
    if not isinstance(version, str) or not version:
        raise CheckError("package.json is missing a non-empty string field: version")

    return {
        "name": name,
        "version": version,
    }


def read_required_text(path, label):
    try:
        return path.read_text(encoding="utf-8")
    except FileNotFoundError:
        raise CheckError("{0} was not found.".format(label))


def assert_runtime_info_has_no_package_metadata_constants(source):
    forbidden_patterns = {
        "PackageName string constant": r"\bconst\s+string\s+PackageName\b",
        "PackageVersion string constant": r"\bconst\s+string\s+PackageVersion\b",
        "runtime package info factory": r"\bGCRuntimeInfo\s+Create\s*\(",
        "runtime package info JSON factory": r"\bstring\s+ToJson\s*\(",
    }

    failures = []
    for label, pattern in forbidden_patterns.items():
        if re.search(pattern, source):
            failures.append(label)

    return failures


def source_contains_string_literal(source, value):
    return _source_contains_string_literal(source, value, 0, len(source))


# Small lexer for exact package-value literals: skip comments, avoid substring
# matches inside larger literals, and recurse into interpolation expressions.
def _source_contains_string_literal(source, value, start_index, end_index):
    index = start_index
    while index < end_index:
        current = source[index]
        next_char = source[index + 1] if index + 1 < end_index else ""

        if current == "/" and next_char == "/":
            index = skip_line_comment(source, index + 2, end_index)
            continue

        if current == "/" and next_char == "*":
            index = skip_block_comment(source, index + 2, end_index)
            continue

        literal = try_read_string_literal(source, index, end_index)
        if literal is not None:
            literal_value, next_index, interpolation_ranges = literal
            if literal_value == value:
                return True

            for interpolation_start, interpolation_end in interpolation_ranges:
                if _source_contains_string_literal(
                    source,
                    value,
                    interpolation_start,
                    interpolation_end,
                ):
                    return True

            index = next_index
            continue

        index += 1

    return False


def try_read_string_literal(source, start_index, end_index):
    literal_start = try_get_string_literal_start(source, start_index, end_index)
    if literal_start is None:
        return None

    quote_index, quote, is_verbatim, is_interpolated = literal_start
    literal_parts = []
    interpolation_ranges = []
    index = quote_index + 1

    while index < end_index:
        current = source[index]
        next_char = source[index + 1] if index + 1 < end_index else ""

        if quote == "`":
            if current == "\\" and index + 1 < end_index:
                literal_parts.append(current)
                literal_parts.append(next_char)
                index += 2
                continue

            if current == "$" and next_char == "{":
                interpolation_end = find_interpolation_end(source, index + 2, end_index)
                if interpolation_end >= 0:
                    interpolation_ranges.append((index + 2, interpolation_end))
                    index = interpolation_end + 1
                    continue

            if current == quote:
                return ("".join(literal_parts), index + 1, interpolation_ranges)

        if quote == '"' and is_interpolated:
            if current == "{" and next_char == "{":
                literal_parts.append(current)
                literal_parts.append(next_char)
                index += 2
                continue

            if current == "{":
                interpolation_end = find_interpolation_end(source, index + 1, end_index)
                if interpolation_end >= 0:
                    interpolation_ranges.append((index + 1, interpolation_end))
                    index = interpolation_end + 1
                    continue

            if current == "}" and next_char == "}":
                literal_parts.append(current)
                literal_parts.append(next_char)
                index += 2
                continue

        if is_verbatim and quote == '"':
            if current == '"' and next_char == '"':
                literal_parts.append(current)
                literal_parts.append(next_char)
                index += 2
                continue

            if current == quote:
                return ("".join(literal_parts), index + 1, interpolation_ranges)
        else:
            if current == "\\" and index + 1 < end_index:
                literal_parts.append(current)
                literal_parts.append(next_char)
                index += 2
                continue

            if current == quote:
                return ("".join(literal_parts), index + 1, interpolation_ranges)

        literal_parts.append(current)
        index += 1

    return ("".join(literal_parts), end_index, interpolation_ranges)


def try_get_string_literal_start(source, start_index, end_index):
    current = source[start_index]
    next_char = source[start_index + 1] if start_index + 1 < end_index else ""
    following = source[start_index + 2] if start_index + 2 < end_index else ""

    if current == "$" and next_char == '"':
        return (start_index + 1, '"', False, True)

    if current == "$" and next_char == "@" and following == '"':
        return (start_index + 2, '"', True, True)

    if current == "@" and next_char == '"':
        return (start_index + 1, '"', True, False)

    if current == "@" and next_char == "$" and following == '"':
        return (start_index + 2, '"', True, True)

    if current in ("'", '"', "`"):
        return (start_index, current, False, current == "`")

    return None


def find_interpolation_end(source, start_index, end_index):
    depth = 0
    index = start_index
    while index < end_index:
        current = source[index]
        next_char = source[index + 1] if index + 1 < end_index else ""

        if current == "/" and next_char == "/":
            index = skip_line_comment(source, index + 2, end_index)
            continue

        if current == "/" and next_char == "*":
            index = skip_block_comment(source, index + 2, end_index)
            continue

        literal = try_read_string_literal(source, index, end_index)
        if literal is not None:
            _, next_index, _ = literal
            index = next_index
            continue

        if current == "{":
            depth += 1
            index += 1
            continue

        if current == "}":
            if depth == 0:
                return index
            depth -= 1
            index += 1
            continue

        index += 1

    return -1


def skip_line_comment(source, start_index, end_index):
    index = start_index
    while index < end_index and source[index] != "\n":
        index += 1
    return index


def skip_block_comment(source, start_index, end_index):
    index = start_index
    while index + 1 < end_index:
        if source[index] == "*" and source[index + 1] == "/":
            return index + 2
        index += 1
    return end_index


def iter_package_code_paths():
    for directory in PACKAGE_CODE_DIRS:
        if not directory.exists():
            continue
        for path in sorted(directory.rglob("*")):
            if path.is_file() and path.suffix in PACKAGE_CODE_SUFFIXES:
                yield path


def assert_manifest_values_not_hardcoded_in_package_sources(manifest):
    failures = []

    for path in iter_package_code_paths():
        source = path.read_text(encoding="utf-8")
        relative_path = path.relative_to(ROOT_DIR)
        if source_contains_string_literal(source, manifest["name"]):
            failures.append("{0} hardcodes package name literal".format(relative_path))
        if source_contains_string_literal(source, manifest["version"]):
            failures.append("{0} hardcodes package version literal".format(relative_path))

    return failures


def assert_runtime_callback_path_removed(runtime_source, bridge_source):
    forbidden_tokens = [
        "GamingCouchRegisterRuntimeInfo",
        "gamingCouchRegisterRuntimeInfo",
        "SendRuntimeInfo",
        "GCRuntimeInfo.ToJson",
    ]

    failures = []
    for token in forbidden_tokens:
        if token in runtime_source:
            failures.append("Runtime/GamingCouch.cs still contains {0}".format(token))
        if token in bridge_source:
            failures.append("Plugins/GamingCouch.jslib still contains {0}".format(token))

    return failures


def main():
    try:
        manifest = read_package_manifest(PACKAGE_JSON_PATH)
        runtime_info_source = read_required_text(
            RUNTIME_INFO_PATH,
            "Runtime/GCRuntimeInfo.cs",
        )
        runtime_source = read_required_text(
            ROOT_DIR / "Runtime" / "GamingCouch.cs",
            "Runtime/GamingCouch.cs",
        )
        bridge_source = read_required_text(
            WEBGL_BRIDGE_PATH,
            "Plugins/GamingCouch.jslib",
        )
    except CheckError as exc:
        print("Could not check runtime package identity cleanup.", file=sys.stderr)
        print(str(exc), file=sys.stderr)
        print("", file=sys.stderr)
        print("Fix: make sure package.json and runtime bridge files exist,", file=sys.stderr)
        print("then rerun: python3 Tools/check-runtime-package-info.py", file=sys.stderr)
        return 2

    failures = []
    failures.extend(assert_runtime_info_has_no_package_metadata_constants(runtime_info_source))
    failures.extend(assert_manifest_values_not_hardcoded_in_package_sources(manifest))
    failures.extend(assert_runtime_callback_path_removed(runtime_source, bridge_source))

    if failures:
        print("Runtime package identity cleanup check failed.", file=sys.stderr)
        for failure in failures:
            print("- {0}".format(failure), file=sys.stderr)
        print("", file=sys.stderr)
        print(
            "Fix: remove runtime package metadata constants/callbacks, then rerun:",
            file=sys.stderr,
        )
        print("python3 Tools/check-runtime-package-info.py", file=sys.stderr)
        return 1

    print("Runtime package identity cleanup check passed.")
    print("- name: {0}".format(manifest["name"]))
    print("- version: {0}".format(manifest["version"]))
    print("- package name/version source: package.json")
    print("- hosted WebGL identity path: gc.runtime-info.json sidecar")
    return 0


if __name__ == "__main__":
    sys.exit(main())
