// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt.Tests.Formatter.CsProj
{
    using PNFmt;

    using PNFmt.Tests.Formatter.CsProj.TestFoundation;

    using PNFmt.Tests.Snapshots;

    using NFluent;

    using System;
    using System.Collections.Generic;
    using System.IO;

    using Xunit;

    public class CsProjSnapshotTests
    {
        [Theory]
        [ClassData(typeof(CsProjSnapshotData))]
        public void Files_are_processed_correctly(string inputFile, string expectedFile, string caseName)
        {
            // Arrange
            var inputRoot = Path.Combine(
                AppContext.BaseDirectory,
                "Formatter",
                "CsProj",
                "_files",
                "input");
            using (var stagedInput = TemporarySnapshotDirectory.CopyFrom(inputRoot))
            {
                var stagedFile = stagedInput.GetPath(
                    caseName.Replace('/', Path.DirectorySeparatorChar));
                var log = new FakeLog();
                var formatter = new CsProjFormatter();

                // Act
                formatter.Format(new FileFormatRequest(stagedFile, true, false, log));

                // Assert
                Check.WithCustomMessage($"Case: {caseName} Input: {inputFile} Expected: {expectedFile}")
                    .That(File.ReadAllText(stagedFile))
                    .Equals(File.ReadAllText(expectedFile));
            }
        }

        internal class CsProjSnapshotData : TheoryDataBase<string, string, string>
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
