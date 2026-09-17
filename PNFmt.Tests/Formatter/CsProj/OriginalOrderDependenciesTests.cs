using System;
using System.Linq;

using Xunit;

namespace PNFmt.Tests.Formatter.CsProj
{
    public sealed class OriginalOrderDependenciesTests
    {
        [Fact]
        public void Sorting_preserves_every_assignment_and_reference_boundary()
        {
            var random = new Random(1729);
            for (var sample = 0; sample < 200; sample++)
            {
                var names = Enumerable.Range(0, 40).Select(_ => "Name" + random.Next(5)).ToArray();
                var references = names.Select(_ => "Name" + random.Next(7)).ToArray();
                var dependencies = new OriginalOrderDependencies(names);
                for (var index = 0; index < names.Length; index++)
                {
                    dependencies.AddReference(index, references[index]);
                }

                var sorted = dependencies.Sort((left, right) => StringComparer.Ordinal.Compare(names[left], names[right])).ToArray();
                Assert.Equal(Enumerable.Range(0, names.Length), sorted.OrderBy(index => index));
                for (var left = 0; left < names.Length; left++)
                {
                    for (var right = left + 1; right < names.Length; right++)
                    {
                        if (names[left] == names[right] || names[left] == references[right] || names[right] == references[left])
                        {
                            Assert.True(Array.IndexOf(sorted, left) < Array.IndexOf(sorted, right));
                        }
                    }
                }
            }
        }
    }
}
