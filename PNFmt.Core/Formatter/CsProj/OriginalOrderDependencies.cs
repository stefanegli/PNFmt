using System;
using System.Collections.Generic;
using System.Linq;

namespace PNFmt
{
    // References must stay between the same assignments, including forward
    // references. Chaining assignments makes their nearest neighbors sufficient:
    // a reference never needs an edge to every repeated definition of its name.
    internal sealed class OriginalOrderDependencies
    {
        private readonly Dictionary<string, List<int>> assignments = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int>[] edges;
        private readonly int[] indegree;

        public OriginalOrderDependencies(IReadOnlyList<string> names)
        {
            this.edges = new HashSet<int>[names.Count];
            this.indegree = new int[names.Count];
            for (var index = 0; index < names.Count; index++)
            {
                this.edges[index] = new HashSet<int>();
                if (!this.assignments.TryGetValue(names[index], out var indices))
                {
                    indices = new List<int>();
                    this.assignments.Add(names[index], indices);
                }

                if (indices.Count > 0)
                {
                    this.AddEdge(indices[indices.Count - 1], index);
                }

                indices.Add(index);
            }
        }

        public void AddReference(int index, string name)
        {
            if (!this.assignments.TryGetValue(name, out var indices))
            {
                return;
            }

            var position = indices.BinarySearch(index);
            if (position >= 0)
            {
                // A self-reference is already constrained by the assignment chain.
                return;
            }

            position = ~position;
            if (position > 0)
            {
                this.AddEdge(indices[position - 1], index);
            }

            if (position < indices.Count)
            {
                this.AddEdge(index, indices[position]);
            }
        }

        public IReadOnlyList<int> Sort(Comparison<int> comparison)
        {
            var remaining = (int[])this.indegree.Clone();
            var ready = new SortedSet<int>(Comparer<int>.Create((left, right) =>
            {
                var order = comparison(left, right);
                return order != 0 ? order : left.CompareTo(right);
            }));
            foreach (var index in Enumerable.Range(0, remaining.Length).Where(index => remaining[index] == 0))
            {
                ready.Add(index);
            }

            var result = new List<int>(remaining.Length);
            while (ready.Count > 0)
            {
                var next = ready.Min;
                ready.Remove(next);
                result.Add(next);
                foreach (var dependent in this.edges[next])
                {
                    if (--remaining[dependent] == 0)
                    {
                        ready.Add(dependent);
                    }
                }
            }

            // Edges always point forward in the input, so cycles are impossible.
            return result;
        }

        private void AddEdge(int earlier, int later)
        {
            if (this.edges[earlier].Add(later))
            {
                this.indegree[later]++;
            }
        }
    }
}
