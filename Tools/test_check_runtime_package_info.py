#!/usr/bin/env python3
"""Contract tests for check-runtime-package-info.py::source_contains_string_literal.

The hardcoded-package-identity gate is only as good as its lexer: it must match an *exact*
executable string literal and nothing else, or the release check either waves through a baked-in
package version or blocks a release over a word in a comment. These cases mirror the C#-side
scanner test (Tests/Editor/GCRuntimeInfoTests.cs,
PackageLiteralScannerDetectsOnlyExactExecutableStringLiterals) so the two scanners stay in step,
and add the branches the C# corpus does not reach: verbatim doubled quotes, interpolation brace
escapes, comment skipping inside an interpolation hole, and nested holes.

Run: python3 Tools/test_check_runtime_package_info.py
"""
import importlib.util
import os
import unittest

CHECKER_PATH = os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "check-runtime-package-info.py"
)

LITERAL = "package.identity.example"


def load_checker():
    spec = importlib.util.spec_from_file_location("check_runtime_package_info", CHECKER_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


checker = load_checker()


class StringLiteralScannerTests(unittest.TestCase):
    def assertHit(self, source):
        self.assertTrue(
            checker.source_contains_string_literal(source, LITERAL),
            "expected a hit in: " + repr(source),
        )

    def assertMiss(self, source):
        self.assertFalse(
            checker.source_contains_string_literal(source, LITERAL),
            "expected no hit in: " + repr(source),
        )


class ExactExecutableLiteralsAreFound(StringLiteralScannerTests):
    """Every quoting form a package source can use to bake in the value."""

    def test_csharp_plain_literal(self):
        self.assertHit('var value = "' + LITERAL + '";')

    def test_csharp_verbatim_literal(self):
        self.assertHit('var value = @"' + LITERAL + '";')

    def test_csharp_interpolated_literal(self):
        self.assertHit('var value = $"' + LITERAL + '";')

    def test_csharp_interpolated_verbatim_literal(self):
        self.assertHit('var value = $@"' + LITERAL + '";')

    def test_csharp_verbatim_interpolated_literal(self):
        self.assertHit('var value = @$"' + LITERAL + '";')

    def test_js_single_quoted_literal(self):
        self.assertHit("var value = '" + LITERAL + "';")

    def test_js_template_literal(self):
        self.assertHit("const value = `" + LITERAL + "`;")

    def test_literal_nested_in_csharp_interpolation_hole(self):
        self.assertHit('var value = $"{ "' + LITERAL + '" }";')

    def test_literal_nested_in_js_template_hole(self):
        self.assertHit('const value = `${"' + LITERAL + '"}`;')

    def test_literal_nested_in_interpolated_verbatim_hole(self):
        self.assertHit('var value = $@"{ "' + LITERAL + '" }";')

    def test_literal_in_hole_with_nested_braces(self):
        self.assertHit('var value = $"{ new[] { "' + LITERAL + '" }[0] }";')

    def test_literal_in_nested_template_hole(self):
        self.assertHit('const value = `${ cond ? `${"' + LITERAL + '"}` : "" }`;')

    def test_literal_in_hole_after_a_block_comment(self):
        self.assertHit('var value = $"{ /* note */ "' + LITERAL + '" }";')

    def test_literal_in_a_concatenation(self):
        self.assertHit('var value = "prefix" + "' + LITERAL + '";')


class NonExecutableOrInexactMatchesAreIgnored(StringLiteralScannerTests):
    """Comments, substrings, and escapes must not trip the gate."""

    def test_line_comment(self):
        self.assertMiss('// "' + LITERAL + '"')

    def test_block_comment(self):
        self.assertMiss("/* `" + LITERAL + "` */")

    def test_whole_statement_inside_a_block_comment(self):
        self.assertMiss('/* var value = "' + LITERAL + '"; */ var other = 1;')

    def test_literal_with_a_prefix(self):
        self.assertMiss('var value = "prefix ' + LITERAL + '";')

    def test_literal_with_a_suffix(self):
        self.assertMiss('var value = "' + LITERAL + ' suffix";')

    def test_escaped_quotes_around_the_value(self):
        self.assertMiss('var value = "\\"' + LITERAL + '\\"";')

    def test_template_literal_with_prefix_and_suffix(self):
        self.assertMiss("var value = `prefix " + LITERAL + " suffix`;")

    def test_template_literal_interpolating_an_identifier(self):
        self.assertMiss("var value = `prefix ${packageName} suffix`;")

    def test_verbatim_doubled_quotes_around_the_value(self):
        self.assertMiss('var value = @"""' + LITERAL + '""";')

    def test_interpolation_brace_escapes_around_the_value(self):
        self.assertMiss('var value = $"{{' + LITERAL + '}}";')

    def test_multiline_verbatim_literal_that_only_contains_the_value(self):
        self.assertMiss('var value = @"line one\n' + LITERAL + '";')

    def test_verbatim_literal_keeps_its_backslashes(self):
        self.assertMiss('var value = @"C:\\' + LITERAL + '";')

    def test_template_literal_with_an_escaped_dollar(self):
        self.assertMiss("const value = `\\${" + LITERAL + "}`;")

    def test_commented_out_literal_inside_an_interpolation_hole(self):
        self.assertMiss('var value = $"{ /* "' + LITERAL + '" */ 1 }";')

    def test_empty_source(self):
        self.assertMiss("")


class ScanningResumesAfterSkippedRegions(StringLiteralScannerTests):
    """A skipped comment or an escape must not swallow the rest of the file."""

    def test_line_comment_does_not_hide_a_later_literal(self):
        self.assertMiss('// "' + LITERAL + '"')
        self.assertHit('// "' + LITERAL + '"\nvar value = "' + LITERAL + '";')

    def test_block_comment_does_not_hide_a_later_literal(self):
        self.assertHit('/* "' + LITERAL + '" */\nvar value = "' + LITERAL + '";')

    def test_apostrophe_in_a_line_comment_does_not_hide_a_later_literal(self):
        self.assertHit('var x = 1; // it\'s fine\nvar value = "' + LITERAL + '";')

    def test_escaped_quote_in_a_char_literal_does_not_hide_a_later_literal(self):
        self.assertHit("var quote = '\\'';\nvar value = \"" + LITERAL + '";')

    def test_division_is_not_mistaken_for_a_comment(self):
        self.assertHit('var r = a / b; var value = "' + LITERAL + '";')


if __name__ == "__main__":
    unittest.main()
