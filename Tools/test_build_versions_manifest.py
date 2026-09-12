#!/usr/bin/env python3
"""Contract tests for the version manifest written at the documentation site root.

The manifest is the only mutable input the published pages have. Every page ever published
reads this one file, and those pages are frozen and cannot be corrected, so a manifest that
names the wrong latest version misleads readers on releases nobody is editing any more.

Run: python3 Tools/test_build_versions_manifest.py
"""
import importlib.util
import os
import unittest

BUILD_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "build-versions-manifest.py")


def load_builder():
    spec = importlib.util.spec_from_file_location("build_versions_manifest", BUILD_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class BuildManifest(unittest.TestCase):
    def setUp(self):
        self.build = load_builder().build_manifest

    def test_this_release_is_listed_even_before_its_folder_is_published(self):
        """The deploy that writes the manifest may run before the API reports its own folder."""
        manifest = self.build([], "0.2.0")
        self.assertEqual(manifest["versions"], ["0.2.0"])
        self.assertEqual(manifest["latestStable"], "0.2.0")

    def test_the_schema_version_is_declared(self):
        # A page that cannot read the manifest must be able to tell, rather than conclude that
        # nothing newer exists.
        self.assertEqual(self.build([], "0.2.0")["schemaVersion"], 1)

    def test_names_that_are_not_versions_are_not_releases(self):
        manifest = self.build(["versions.json", "assets", "latest", "0.1", "1.0.0"], "0.2.0")
        self.assertEqual(manifest["versions"], ["0.2.0", "1.0.0"])

    def test_versions_are_ordered_by_semver_precedence(self):
        manifest = self.build(["0.2.0", "0.10.0", "0.9.0"], "0.3.0")
        self.assertEqual(manifest["versions"], ["0.2.0", "0.3.0", "0.9.0", "0.10.0"])

    def test_a_release_outranks_its_own_prerelease(self):
        manifest = self.build(["1.0.0-rc.1", "1.0.0"], "0.9.0")
        self.assertEqual(manifest["versions"], ["0.9.0", "1.0.0-rc.1", "1.0.0"])
        self.assertEqual(manifest["latestStable"], "1.0.0")

    def test_the_latest_stable_ignores_newer_prereleases(self):
        manifest = self.build(["1.0.0", "1.1.0-beta.1"], "1.0.0")
        self.assertEqual(manifest["latestStable"], "1.0.0")

    def test_there_is_no_latest_stable_while_only_prereleases_exist(self):
        manifest = self.build(["0.1.0-alpha.7"], "0.1.0-alpha.8")
        self.assertIsNone(manifest["latestStable"])

    def test_republishing_a_version_does_not_list_it_twice(self):
        manifest = self.build(["0.1.0", "0.2.0"], "0.2.0")
        self.assertEqual(manifest["versions"], ["0.1.0", "0.2.0"])

    def test_build_metadata_is_kept_in_the_name_but_not_in_precedence(self):
        # The folder is named for the tag, so the name has to survive verbatim or the manifest
        # would point at a folder that does not exist.
        manifest = self.build(["1.0.0"], "1.0.1+build.5")
        self.assertIn("1.0.1+build.5", manifest["versions"])

    def test_no_notice_is_invented(self):
        # A relocation notice is a deliberate statement, added when documentation genuinely
        # moves. A routine deploy has nothing to say.
        self.assertNotIn("notice", self.build([], "0.2.0"))


if __name__ == "__main__":
    unittest.main()
