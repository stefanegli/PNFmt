// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;

using LibGit2Sharp;

using PNFmt.Tests.Snapshots;

using var repository = new Repository(Repository.Discover(AppContext.BaseDirectory)
    ?? throw new InvalidOperationException("Run the exporter from a PNFmt checkout."));
var root = repository.Info.WorkingDirectory;
var wrappingReviewOnly = args.Contains("--wrapping-review");
var paths = args.Where(argument => argument != "--wrapping-review").ToArray();
if (paths.Length > 1 || paths.Any(argument => argument.StartsWith("--", StringComparison.Ordinal)))
{
    throw new ArgumentException("Usage: SnapshotViewer [output.html] [--wrapping-review]");
}
var output = paths.Length == 0
    ? Path.Combine(root, "artifacts", wrappingReviewOnly ? "wrapping-review.html" : "snapshot-review.html") : Path.GetFullPath(paths[0]);
SnapshotReviewExporter.Write(root, output, wrappingReviewOnly);
Console.WriteLine(output);
