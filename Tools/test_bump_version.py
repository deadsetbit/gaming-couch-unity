#!/usr/bin/env python3
"""Contract tests for the docs URLs bump-version.py writes into a release.

What a release ships in these fields is frozen the moment its tag is pushed, and the plan
forbids republishing a tag, so a version that goes out naming the wrong folder points every
pinned consumer at the wrong documentation for good. That makes these rewrites worth testing
directly rather than through the dry run, which returns before any of them happen.

Run: python3 Tools/test_bump_version.py
"""
import importlib.util
import os
import tempfile
import unittest
from pathlib import Path

BUMP_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "bump-version.py")

PACKAGE_JSON = """{
          "name": "com.dsb.gamingcouch",
          "version": "0.1.0-alpha.7",
          "documentationUrl": "https://deadsetbit.github.io/gaming-couch-unity-public/",
          "changelogUrl": "https://deadsetbit.github.io/gaming-couch-unity-public/latest/changelog/CHANGELOG.html",
          "licensesUrl": "https://deadsetbit.github.io/gaming-couch-unity-public/latest/license/LICENSE.html",
          "license": "Apache-2.0"
}
"""


def load_bump():
    spec = importlib.util.spec_from_file_location("bump_version", BUMP_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bump = load_bump()


class ManifestUrlsNameTheReleaseFolder(unittest.TestCase):
    def rewrite(self, version):
        return bump.rewrite_docs_urls(PACKAGE_JSON, version)

    def test_all_three_fields_point_into_the_version_folder(self):
        result = self.rewrite("0.2.0")
        self.assertIn(
            '"documentationUrl": "https://deadsetbit.github.io/gaming-couch-unity-public/0.2.0/"',
            result,
        )
        self.assertIn(
            '"changelogUrl": "https://deadsetbit.github.io/gaming-couch-unity-public/0.2.0/changelog/CHANGELOG.html"',
            result,
        )
        self.assertIn(
            '"licensesUrl": "https://deadsetbit.github.io/gaming-couch-unity-public/0.2.0/license/LICENSE.html"',
            result,
        )

    def test_a_prerelease_gets_its_own_folder_like_any_other_release(self):
        self.assertIn(
            '"documentationUrl": "https://deadsetbit.github.io/gaming-couch-unity-public/0.2.0-beta.1/"',
            self.rewrite("0.2.0-beta.1"),
        )

    def test_no_channel_name_survives_the_rewrite(self):
        self.assertNotIn("/latest/", self.rewrite("0.2.0"))

    def test_the_manifest_keeps_its_formatting_and_its_other_fields(self):
        result = self.rewrite("0.2.0")
        self.assertIn('          "name": "com.dsb.gamingcouch",', result)
        self.assertIn('"license": "Apache-2.0"', result)
        self.assertEqual(len(PACKAGE_JSON.splitlines()), len(result.splitlines()))

    def test_a_missing_field_is_refused_rather_than_skipped(self):
        with self.assertRaises(bump.BumpError):
            bump.rewrite_docs_urls('{"documentationUrl": "x", "changelogUrl": "y"}', "0.2.0")


class DeepLinksFollowTheReleaseFolder(unittest.TestCase):
    def sweep(self, files, version="0.2.0"):
        """Run the sweep over a throwaway package tree and return what it left behind."""
        with tempfile.TemporaryDirectory() as tmp:
            package_dir = Path(tmp) / "public" / "package"
            package_dir.mkdir(parents=True)
            for name, text in files.items():
                path = package_dir / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(text, encoding="utf-8")

            original_dir = bump.PACKAGE_DIR
            bump.PACKAGE_DIR = package_dir
            try:
                originals = {}
                bump.rewrite_docs_deep_links(version, originals)
                return (
                    {name: (package_dir / name).read_text(encoding="utf-8") for name in files},
                    {path.name for path in originals},
                )
            finally:
                bump.PACKAGE_DIR = original_dir

    def test_an_api_deep_link_is_repointed_at_the_new_folder(self):
        written, _ = self.sweep(
            {"README.md": "see [the API](https://deadsetbit.github.io/gaming-couch-unity-public/latest/api/DSB.GC.GCPlayer.html)."}
        )
        self.assertIn(
            "https://deadsetbit.github.io/gaming-couch-unity-public/0.2.0/api/DSB.GC.GCPlayer.html",
            written["README.md"],
        )

    def test_a_link_to_the_api_index_is_repointed_too(self):
        written, _ = self.sweep(
            {"README.md": "- [API](https://deadsetbit.github.io/gaming-couch-unity-public/latest/api)\n"}
        )
        self.assertIn("gaming-couch-unity-public/0.2.0/api)", written["README.md"])

    def test_a_previous_release_folder_is_repointed_at_this_one(self):
        written, _ = self.sweep(
            {"README.md": "https://deadsetbit.github.io/gaming-couch-unity-public/0.1.9/api/X.html"}
        )
        self.assertIn("gaming-couch-unity-public/0.2.0/api/X.html", written["README.md"])

    def test_nested_documentation_is_swept_as_well(self):
        written, touched = self.sweep(
            {"Documentation~/README.md": "https://deadsetbit.github.io/gaming-couch-unity-public/latest/api\n"}
        )
        self.assertIn("/0.2.0/api", written["Documentation~/README.md"])
        self.assertEqual(touched, {"README.md"})

    def test_the_changelog_is_left_alone(self):
        entry = "API links are deep links under `https://deadsetbit.github.io/gaming-couch-unity-public/latest/api`.\n"
        written, touched = self.sweep({"CHANGELOG.md": entry})
        self.assertEqual(written["CHANGELOG.md"], entry)
        self.assertEqual(touched, set())

    def test_the_site_root_on_its_own_is_not_a_deep_link(self):
        root = "Docs live at https://deadsetbit.github.io/gaming-couch-unity-public/.\n"
        written, touched = self.sweep({"README.md": root})
        self.assertEqual(written["README.md"], root)
        self.assertEqual(touched, set())

    def test_links_that_are_not_the_api_reference_follow_too(self):
        """A superseded manual or changelog page still renders, describing the wrong release.

        That is worse than a dead link: nothing about the page looks wrong to a reader.
        """
        for path in ("changelog/CHANGELOG.html", "license/LICENSE.html", "manual/index.html"):
            with self.subTest(path=path):
                written, _ = self.sweep(
                    {"README.md": "https://deadsetbit.github.io/gaming-couch-unity-public/0.1.9/" + path}
                )
                self.assertIn("gaming-couch-unity-public/0.2.0/" + path, written["README.md"])

    def test_a_bare_release_folder_link_follows(self):
        written, _ = self.sweep(
            {"README.md": "[docs](https://deadsetbit.github.io/gaming-couch-unity-public/0.1.9/)"}
        )
        self.assertIn("gaming-couch-unity-public/0.2.0/)", written["README.md"])

    def test_a_file_at_the_site_root_is_not_a_release_folder(self):
        """The version manifest lives at the root and belongs to no release."""
        manifest = "https://deadsetbit.github.io/gaming-couch-unity-public/versions.json\n"
        written, touched = self.sweep({"README.md": manifest})
        self.assertEqual(written["README.md"], manifest)
        self.assertEqual(touched, set())

    def test_the_previous_contents_are_recorded_before_the_file_is_written(self):
        before = "https://deadsetbit.github.io/gaming-couch-unity-public/latest/api\n"
        with tempfile.TemporaryDirectory() as tmp:
            package_dir = Path(tmp) / "public" / "package"
            package_dir.mkdir(parents=True)
            (package_dir / "README.md").write_text(before, encoding="utf-8")

            original_dir = bump.PACKAGE_DIR
            bump.PACKAGE_DIR = package_dir
            try:
                originals = {}
                bump.rewrite_docs_deep_links("0.2.0", originals)
            finally:
                bump.PACKAGE_DIR = original_dir

            for path, text in originals.items():
                path.write_text(text, encoding="utf-8")
            self.assertEqual((package_dir / "README.md").read_text(encoding="utf-8"), before)


if __name__ == "__main__":
    unittest.main()
