// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;

using LibGit2Sharp;

using PNFmt.Tests.Snapshots;

using var repository = new Repository(Repository.Discover(AppContext.BaseDirectory)
    ?? throw new InvalidOperationException("Run the exporter from a PNFmt checkout."));
var root = repository.Info.WorkingDirectory;
var output = args.Length == 0 ? Path.Combine(root, "artifacts", "snapshot-review.html") : Path.GetFullPath(args[0]);
SnapshotReviewExporter.Write(root, output);
Console.WriteLine(output);
