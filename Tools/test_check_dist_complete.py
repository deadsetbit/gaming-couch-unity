#!/usr/bin/env python3
"""Contract tests for check-dist-complete.py.

This gate is the last thing between the private repository and the public internet, and every
rule in it fails the publish. Two of them fail against a *correct* package if written naively:
meta pairing has exactly one legitimate exception, and assembly references cannot all resolve
inside the package. Those two carry the most cases here.

Each test builds a synthetic package tree, so the cases say what shape of package passes and
what shape is refused, rather than restating today's file list.

Run: python3 Tools/test_check_dist_complete.py
"""
import contextlib
import importlib.util
import io
import json
import os
import shutil
import tempfile
import unittest
from pathlib import Path

GATE_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "check-dist-complete.py")


def load_gate():
    spec = importlib.util.spec_from_file_location("check_dist_complete", GATE_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


gate = load_gate()

PACKAGE_NAME = "com.dsb.gamingcouch"
VERSION = "0.1.0-alpha.9"
TAG = "unity-" + VERSION


class PackageTreeTestCase(unittest.TestCase):
    """A minimal package that passes every structural rule, for each case to break one way."""

    def setUp(self):
        self.root = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)
        self.write_manifest()
        self.write_asset("README.md", "# readme\n")
        self.write_asset("CHANGELOG.md", "# changelog\n")
        self.write_asset("LICENSE.md", "Apache-2.0\n")
        self.write_asset("ContractFixtures/LocalPlay/valid-case/gc.dev.json", "{}\n")
        self.write_asmdef("Runtime/gc.runtime.asmdef", "GamingCouch", [], guid="a" * 32)
        self.write_asset("Editor/GCEditor.cs", "// editor\n")
        self.write_asset("Plugins/GamingCouch.jslib", "// bridge\n")
        self.write_asset("Tests/Editor/GCTests.cs", "// tests\n")
        (self.root / "Documentation~").mkdir()
        (self.root / "Documentation~" / "README.md").write_text("# manual\n", encoding="utf-8")

    def write_manifest(self, **overrides):
        manifest = {"name": PACKAGE_NAME, "version": VERSION}
        manifest.update(overrides)
        self.write_asset("package.json", json.dumps(manifest, indent=2) + "\n")

    def write_asset(self, relative_path, content, guid=None):
        path = self.root / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
        self.write_meta(relative_path, guid)
        # Unity gives every folder a meta too, so the fixture has to as well.
        for parent in path.parents:
            if parent == self.root:
                break
            self.write_meta(str(parent.relative_to(self.root)), None)

    def write_meta(self, relative_path, guid):
        meta = self.root / (relative_path + ".meta")
        if meta.exists():
            return
        meta.parent.mkdir(parents=True, exist_ok=True)
        meta.write_text("fileFormatVersion: 2\nguid: {0}\n".format(guid or "0" * 32), encoding="utf-8")

    def write_asmdef(self, relative_path, name, references, guid):
        body = {"name": name, "references": references}
        self.write_asset(relative_path, json.dumps(body, indent=2) + "\n", guid=guid)

    def check(self, tag=TAG):
        failures, _ = gate.run_checks(self.root, tag)
        return failures

    def assertPasses(self):
        failures = self.check()
        self.assertEqual(failures, [], "expected a clean package, got: {0}".format(failures))

    def assertFailsWith(self, *fragments):
        failures = self.check()
        self.assertTrue(failures, "expected a failure, package passed")
        joined = "\n".join(failures)
        for fragment in fragments:
            self.assertIn(fragment, joined)


class AKnownGoodPackagePasses(PackageTreeTestCase):
    def test_the_fixture_package_passes_every_structural_rule(self):
        self.assertPasses()


class ManifestIdentity(PackageTreeTestCase):
    def test_missing_manifest_fails(self):
        (self.root / "package.json").unlink()
        (self.root / "package.json.meta").unlink()
        self.assertFailsWith("package.json", "not found")

    def test_unparseable_manifest_fails(self):
        (self.root / "package.json").write_text("{not json", encoding="utf-8")
        self.assertFailsWith("package.json", "not valid JSON")

    def test_wrong_package_name_fails(self):
        self.write_manifest(name="com.example.other")
        self.assertFailsWith("com.example.other", PACKAGE_NAME)

    def test_version_not_matching_the_tag_fails(self):
        self.write_manifest(version="0.1.0-alpha.8")
        self.assertFailsWith("0.1.0-alpha.8", TAG)

    def test_tag_without_the_unity_prefix_fails(self):
        failures = self.check(tag="v0.1.0-alpha.9")
        self.assertTrue(any("unity-" in failure for failure in failures), failures)


class MetaPairing(PackageTreeTestCase):
    def test_an_asset_without_a_meta_fails(self):
        (self.root / "README.md.meta").unlink()
        self.assertFailsWith("README.md", "no .meta")

    def test_a_meta_without_its_asset_fails(self):
        (self.root / "README.md").unlink()
        self.assertFailsWith("README.md.meta", "no asset")

    def test_a_folder_without_a_meta_fails(self):
        (self.root / "Runtime.meta").unlink()
        self.assertFailsWith("Runtime", "no .meta")

    def test_a_top_level_folder_is_not_exempt(self):
        # A bad `git mv` loses exactly these, and they carry the GUIDs asmdef references
        # resolve against, so the gate must not skip them.
        (self.root / "ContractFixtures.meta").unlink()
        self.assertFailsWith("ContractFixtures", "no .meta")

    def test_a_nested_folder_without_a_meta_fails(self):
        (self.root / "Tests" / "Editor.meta").unlink()
        self.assertFailsWith("Tests/Editor", "no .meta")

    def test_a_tilde_folder_needs_no_metas(self):
        # Unity hides a path with a `~` segment, so Documentation~ has no metas by design —
        # and the fixture deliberately gives it none.
        self.assertFalse((self.root / "Documentation~.meta").exists())
        self.assertFalse((self.root / "Documentation~" / "README.md.meta").exists())
        self.assertPasses()

    def test_a_file_inside_a_tilde_folder_needs_no_meta(self):
        (self.root / "Documentation~" / "extra.md").write_text("x\n", encoding="utf-8")
        self.assertPasses()

    def test_ds_store_is_ignored(self):
        (self.root / ".DS_Store").write_bytes(b"\x00")
        self.assertPasses()


class AsmdefReferences(PackageTreeTestCase):
    def test_a_guid_reference_resolving_inside_the_package_passes(self):
        self.write_asmdef(
            "Editor/gc.editor.asmdef", "GamingCouch.Editor", ["GUID:" + "a" * 32], guid="b" * 32
        )
        self.assertPasses()

    def test_a_guid_reference_resolving_nowhere_fails(self):
        self.write_asmdef(
            "Editor/gc.editor.asmdef", "GamingCouch.Editor", ["GUID:" + "f" * 32], guid="b" * 32
        )
        self.assertFailsWith("f" * 32, "does not resolve")

    def test_a_by_name_reference_to_an_internal_assembly_passes(self):
        self.write_asmdef(
            "Editor/gc.editor.asmdef", "GamingCouch.Editor", ["GamingCouch"], guid="b" * 32
        )
        self.assertPasses()

    def test_an_allowlisted_external_reference_passes(self):
        self.write_asmdef(
            "Editor/gc.editor.asmdef", "GamingCouch.Editor", ["Unity.Newtonsoft.Json"], guid="b" * 32
        )
        self.assertPasses()

    def test_an_unlisted_external_reference_fails(self):
        self.write_asmdef(
            "Editor/gc.editor.asmdef", "GamingCouch.Editor", ["Some.Other.Package"], guid="b" * 32
        )
        self.assertFailsWith("Some.Other.Package", "not on the external allowlist")

    def test_an_unparseable_asmdef_fails(self):
        self.write_asset("Editor/gc.editor.asmdef", "{not json", guid="b" * 32)
        self.assertFailsWith("gc.editor.asmdef", "not a valid assembly definition object")

    def test_the_allowlist_is_consulted_by_an_allowed_reference(self):
        self.write_asmdef(
            "Editor/gc.editor.asmdef", "GamingCouch.Editor", ["Unity.Newtonsoft.Json"], guid="b" * 32
        )
        _, consulted = gate.run_checks(self.root, TAG)
        self.assertTrue(consulted)

    def test_the_allowlist_is_consulted_by_a_reference_that_fails_against_it(self):
        # The moment a reader most needs to see the allowlist is the moment one fails it.
        self.write_asmdef(
            "Editor/gc.editor.asmdef", "GamingCouch.Editor", ["Some.Other.Package"], guid="b" * 32
        )
        _, consulted = gate.run_checks(self.root, TAG)
        self.assertTrue(consulted)

    def test_the_allowlist_is_not_consulted_when_every_reference_is_internal(self):
        self.write_asmdef(
            "Editor/gc.editor.asmdef", "GamingCouch.Editor", ["GamingCouch"], guid="b" * 32
        )
        _, consulted = gate.run_checks(self.root, TAG)
        self.assertFalse(consulted)

    def test_the_printed_allowlist_names_every_entry_and_its_reason(self):
        printed = gate.format_external_allowlist()
        for name in ("Unity.Newtonsoft.Json", "UnityEngine.UI", "Unity.Netcode.Runtime"):
            self.assertIn(name, printed)


class ContractFixtures(PackageTreeTestCase):
    def test_a_missing_corpus_fails(self):
        # The shipped tests reach the corpus by string path, so no reference check can see it.
        shutil.rmtree(self.root / "ContractFixtures" / "LocalPlay")
        (self.root / "ContractFixtures" / "LocalPlay.meta").unlink()
        self.assertFailsWith("ContractFixtures/LocalPlay", "missing")

    def test_an_empty_corpus_fails(self):
        for child in (self.root / "ContractFixtures" / "LocalPlay").iterdir():
            shutil.rmtree(child) if child.is_dir() else child.unlink()
        self.assertFailsWith("ContractFixtures/LocalPlay", "empty")


class ScannerSuppressionInsideThePackage(PackageTreeTestCase):
    """A scanner config inside the scanned folder turns the leak backstop off silently."""

    def test_a_scanner_config_in_the_package_fails(self):
        self.write_asset(".gitleaks.toml", "title = 'neutered'\n")
        self.assertFailsWith(".gitleaks.toml", "suppress the scan")

    def test_a_scanner_ignore_file_in_the_package_fails(self):
        self.write_asset(".gitleaksignore", "somefinding\n")
        self.assertFailsWith(".gitleaksignore", "suppress the scan")

    def test_a_nested_scanner_config_fails(self):
        self.write_asset("Runtime/.gitleaks.toml", "title = 'neutered'\n")
        self.assertFailsWith(".gitleaks.toml", "suppress the scan")


class SecretScan(unittest.TestCase):
    """The scan shells out to gitleaks; these cases pin the contract around that call."""

    def test_a_missing_scanner_fails_closed(self):
        failures = gate.scan_for_secrets(Path("/nonexistent"), scanner="definitely-not-installed")
        self.assertTrue(failures)
        self.assertIn("definitely-not-installed", "\n".join(failures))

    def test_the_invocation_is_directory_mode(self):
        # gitleaks defaults to scanning git history; a folder scan needs its directory mode,
        # and the published tree is a folder with no history of its own.
        argv = gate.build_secret_scan_command(Path("/tmp/pkg"), scanner="gitleaks")
        self.assertEqual(argv[:2], ["gitleaks", "dir"])
        self.assertIn("/tmp/pkg", argv)

    def test_findings_use_an_exit_code_gitleaks_does_not_also_use_for_its_own_errors(self):
        # gitleaks exits 1 on a usage or IO error. Leaving findings on 1 too would make a
        # broken invocation read as a leak and trigger a needless credential rotation.
        argv = gate.build_secret_scan_command(Path("/tmp/pkg"))
        self.assertEqual(argv[argv.index("--exit-code") + 1], "2")
        self.assertNotEqual(gate.SECRET_SCAN_LEAK_EXIT_CODE, 1)

    def test_in_tree_suppression_is_disabled(self):
        # A `gitleaks:allow` comment in a shipped file would otherwise exempt that file from
        # the only leak backstop the publish has, and a symlink would hide its content.
        argv = gate.build_secret_scan_command(Path("/tmp/pkg"))
        self.assertIn("--ignore-gitleaks-allow", argv)
        self.assertIn("--follow-symlinks", argv)

    def test_the_ignore_path_is_not_the_scanned_folder(self):
        # Pointed at the package, the ignore-file loader honours a `.gitleaksignore` sitting
        # inside it — which is the suppression this is supposed to prevent.
        argv = gate.build_secret_scan_command(Path("/tmp/pkg"))
        self.assertNotEqual(argv[argv.index("--gitleaks-ignore-path") + 1], "/tmp/pkg")

    def test_the_required_scanner_version_is_where_directory_mode_appeared(self):
        self.assertGreaterEqual(gate.MINIMUM_SCANNER_VERSION, (8, 19, 0))


class RequiredContents(PackageTreeTestCase):
    """Meta pairing sees only half a loss, so a whole folder going missing needs its own rule."""

    def test_a_missing_required_directory_fails(self):
        shutil.rmtree(self.root / "Runtime")
        (self.root / "Runtime.meta").unlink()
        self.assertFailsWith("Runtime", "required directory is missing")

    def test_an_empty_required_directory_fails(self):
        for child in list((self.root / "Plugins").iterdir()):
            child.unlink()
        self.assertFailsWith("Plugins", "required directory is empty")

    def test_a_missing_required_file_fails(self):
        (self.root / "CHANGELOG.md").unlink()
        (self.root / "CHANGELOG.md.meta").unlink()
        self.assertFailsWith("CHANGELOG.md", "required file is missing")

    def test_an_empty_required_file_fails(self):
        (self.root / "LICENSE.md").write_text("", encoding="utf-8")
        self.assertFailsWith("LICENSE.md", "required file is empty")

    def test_a_package_stripped_of_everything_but_the_manifest_fails(self):
        for name in ("Runtime", "Editor", "Plugins", "Tests", "Documentation~"):
            shutil.rmtree(self.root / name)
        self.assertFailsWith("required directory is missing")

    def test_a_manifest_that_is_valid_json_but_not_an_object_fails(self):
        (self.root / "package.json").write_text("[]\n", encoding="utf-8")
        self.assertFailsWith("package.json", "not an object")


class Symlinks(PackageTreeTestCase):
    def test_a_symlink_in_the_package_fails(self):
        # It reads as an ordinary file to the walk while its content lives outside the package.
        target = self.root.parent / "outside.cs"
        target.write_text("// outside\n", encoding="utf-8")
        self.addCleanup(target.unlink)
        link = self.root / "Runtime" / "Linked.cs"
        link.symlink_to(target)
        self.write_meta("Runtime/Linked.cs", None)
        self.assertFailsWith("Runtime/Linked.cs", "symlink")


class ContractFixtureCases(PackageTreeTestCase):
    def test_a_case_without_the_project_file_fails(self):
        (self.root / "ContractFixtures" / "LocalPlay" / "valid-case" / "gc.dev.json").unlink()
        self.assertFailsWith("valid-case", "no gc.dev.json")

    def test_a_stray_file_where_a_case_should_be_fails(self):
        self.write_asset("ContractFixtures/LocalPlay/stray.txt", "x\n")
        self.assertFailsWith("stray.txt", "not a fixture case directory")


class SecretScanBehaviour(unittest.TestCase):
    """The exit-code mapping is the part that decides whether a leak stops a release."""

    def stub_scanner(self, exit_code, version="8.30.1"):
        directory = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, directory, ignore_errors=True)
        script = directory / "stub-scanner"
        script.write_text(
            "#!/bin/sh\n"
            'if [ "$1" = "version" ]; then echo {0}; exit 0; fi\n'
            "echo stub output\nexit {1}\n".format(version, exit_code),
            encoding="utf-8",
        )
        script.chmod(0o755)
        original_path = os.environ["PATH"]
        self.addCleanup(os.environ.__setitem__, "PATH", original_path)
        os.environ["PATH"] = str(directory) + os.pathsep + original_path
        return "stub-scanner"

    def test_a_clean_scan_reports_nothing(self):
        self.assertEqual(gate.scan_for_secrets(Path("/tmp"), self.stub_scanner(0)), [])

    def test_the_findings_exit_code_reports_a_leak(self):
        failures = gate.scan_for_secrets(Path("/tmp"), self.stub_scanner(2))
        self.assertIn("rotate", "\n".join(failures))

    def test_any_other_failure_reports_that_nothing_was_scanned(self):
        # gitleaks exits 1 on its own usage and IO errors. Reporting that as a leak would
        # trigger a needless credential rotation.
        failures = gate.scan_for_secrets(Path("/tmp"), self.stub_scanner(1))
        joined = "\n".join(failures)
        self.assertIn("never", joined)
        self.assertNotIn("rotate", joined)

    def test_a_scanner_too_old_for_directory_mode_is_refused(self):
        failures = gate.scan_for_secrets(Path("/tmp"), self.stub_scanner(0, version="8.18.4"))
        self.assertIn("older than", "\n".join(failures))


class CommandLine(PackageTreeTestCase):
    """The exit code is the whole contract: the publish workflow reads nothing else."""

    def run_main(self):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = gate.main(["check-dist-complete.py", str(self.root), TAG])
        return code, out.getvalue(), err.getvalue()

    def test_wrong_argument_count_exits_two(self):
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(gate.main(["check-dist-complete.py"]), 2)

    def test_a_failure_exits_one_and_prints_every_reason_to_stderr(self):
        (self.root / "CHANGELOG.md").unlink()
        (self.root / "CHANGELOG.md.meta").unlink()
        code, _, err = self.run_main()
        self.assertEqual(code, 1)
        self.assertIn("CHANGELOG.md: required file is missing", err)
        self.assertIn("Nothing is published.", err)

    def test_the_allowlist_prints_even_when_a_reference_fails_against_it(self):
        self.write_asmdef(
            "Editor/gc.editor.asmdef", "GamingCouch.Editor", ["Some.Other.Package"], guid="b" * 32
        )
        code, out, err = self.run_main()
        self.assertEqual(code, 1)
        self.assertIn("Unity.Newtonsoft.Json", out)
        self.assertIn("not on the external allowlist", err)


if __name__ == "__main__":
    unittest.main()
