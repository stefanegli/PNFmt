// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpExclusionTests
    {
        [Fact]
        public void Excluded_code_and_markers_are_exact_while_surrounding_code_is_formatted()
        {
            const string Protected = " // pnfmt: off  \r\nvoid   Keep( ){   }  \r\n // pnfmt: on\r\n";
            var input = "class C{\nvoid Before(){ }\n" + Protected + "void After(){ }\n}\n";
            var result = Format(input);

            Assert.Contains(Protected, result);
            Assert.Contains("    void Before()", result);
            Assert.Contains("    void After()", result);
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Imports_inside_exclusions_are_not_sorted()
        {
            const string Protected = "// pnfmt: off\nusing   Z;\nusing   A;\n// pnfmt: on\n";
            var result = Format("using Y;\nusing B;\n" + Protected + "class C{ }\n");
            Assert.StartsWith("using B;\nusing Y;", result);
            Assert.Contains(Protected, result);
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Nested_exclusions_and_unclosed_exclusions_preserve_everything_to_the_end()
        {
            const string Protected = "// pnfmt: off\nclass C{\n// pnfmt: off\nvoid M(){ }\n// pnfmt: on\n}";
            var result = Format("using Z;\nusing A;\n" + Protected);
            Assert.Equal("using A;\nusing Z;\n" + Protected, result);
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Marker_text_inside_literals_or_trailing_comments_does_not_disable_formatting()
        {
            const string Input = "class C{\nstring s=\"// pnfmt: off\"; // pnfmt: off\nvoid M(){ }\n}\n";
            var result = Format(Input);
            Assert.Contains("    void M()", result);
            Assert.Contains("string s = \"// pnfmt: off\"", result);
        }

        [Fact]
        public void Unmatched_on_marker_does_not_disable_formatting()
        {
            Assert.Contains("    void M()", Format("// pnfmt: on\nclass C{\nvoid M(){ }\n}\n"));
        }

        private static string Format(string text)
        {
            return CSharpFormatterTests.Format(text, ("pnfmt_sort_entries", "true"),
                ("trim_trailing_whitespace", "true"), ("insert_final_newline", "true"), ("end_of_line", "lf"));
        }
    }
}
