// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt.Tests.Formatter.CsProj
{
    using PNFmt;

    using PNFmt.Tests.Formatter.CsProj.TestFoundation;

    using NFluent;

    using System;
    using System.Collections.Generic;
    using System.IO;

    using Xunit;

    public class FormattingTests
    {
        [Theory]
        [ClassData(typeof(CsProjTestData))]
        public void Files_are_processed_correctly(string inputFile, string expectedFile, string caseName)
        {
            // Arrange
            var log = new FakeLog();
            var formatter = new CsProjFormatter();

            // Act
            formatter.Format(new FileFormatRequest(inputFile, true, false, log));

            // Assert
            Check.WithCustomMessage($"Case: {caseName} Input: {inputFile} Expected: {expectedFile}")
                .That(File.ReadAllText(inputFile))
                .Equals(File.ReadAllText(expectedFile));
        }

        internal class CsProjTestData : TheoryDataBase<string, string, string>
        {
            public override IEnumerable<(string, string, string)> Create()
            {
                var outputRoot = Path.Combine(
                    AppContext.BaseDirectory,
                    "Formatter",
                    "CsProj",
                    "_files");
                foreach (var testCase in FormattingCaseSource.Create(outputRoot))
                {
                    yield return (testCase.InputFile, testCase.ExpectedFile, testCase.CaseName);
                }
            }
        }
    }
}
