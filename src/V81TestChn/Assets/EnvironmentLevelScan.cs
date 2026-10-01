using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace V81TestChn;

// Each MoveNext discovers one node/child or returns one renderer. In particular,
// a root with thousands of descendants never does GetComponentsInChildren.
internal sealed class EnvironmentLevelScan : IDisposable
{
    private struct NodeCursor
    {
        internal Transform Node;
        internal int NextChild;
        internal int ObservedChildCount;
        internal Transform? PreviousChild;
        internal NodeCursor(Transform node)
        { Node = node; NextChild = 0; ObservedChildCount = 0; PreviousChild = null; }
    }

    private sealed class TransformIdentityComparer : IEqualityComparer<Transform>
    {
        internal static readonly TransformIdentityComparer Instance = new();
        public bool Equals(Transform? left, Transform? right) => ReferenceEquals(left, right);
        public int GetHashCode(Transform value) => RuntimeHelpers.GetHashCode(value);
    }

    private static readonly Func<long> Timestamp = Stopwatch.GetTimestamp;
    private readonly Action<Renderer> _apply;
    private IEnumerator<Renderer?>? _work;

    internal EnvironmentLevelScan(Action<Renderer> apply)
    {
        _apply = apply;
        _work = Enumerate().GetEnumerator();
    }

    internal bool Pump() => Pump(256, Math.Max(1, Stopwatch.Frequency / 2000), Timestamp);

    // The time check is between Unity operations, never a promise that one
    // native texture decode/upload or scene root enumeration can be interrupted.
    internal bool Pump(int maximumWorkItems, long maximumTicks, Func<long> timestamp)
    {
        if (_work == null) return false;
        var start = timestamp();
        for (var index = 0; index < maximumWorkItems; index++)
        {
            if (!_work.MoveNext())
            {
                Dispose();
                return false;
            }

            var renderer = _work.Current;
            if (renderer != null) _apply(renderer);
            if (_work == null) return false; // cancellation during a callback
            if (timestamp() - start >= maximumTicks) return true;
        }
        return true;
    }

    public void Dispose()
    {
        var work = _work;
        _work = null;
        work?.Dispose();
    }

    private static IEnumerable<Renderer?> Enumerate()
    {
        var roots = new List<GameObject>();
        var renderers = new List<Renderer>();
        var nodes = new Stack<NodeCursor>();
        // Recorded lazily as nodes are visited, not an up-front tree snapshot.
        // Identity comparisons also avoid treating a reused native ID as visited.
        var visited = new HashSet<Transform>(TransformIdentityComparer.Instance);
        try
        {
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                roots.Clear();
                if (scene.IsValid() && scene.isLoaded)
                {
                    // Unity exposes roots as a bulk operation, but descendants
                    // and renderer work below remain individually scheduled.
                    if (roots.Capacity < scene.rootCount) roots.Capacity = scene.rootCount;
                    scene.GetRootGameObjects(roots);
                }
                yield return null;

                for (var rootIndex = 0; rootIndex < roots.Count; rootIndex++)
                {
                    if (!scene.IsValid() || !scene.isLoaded) break;
                    var root = roots[rootIndex];
                    if (root == null) { yield return null; continue; }
                    Transform? next = root.transform;
                    while (next != null || nodes.Count > 0)
                    {
                        if (!scene.IsValid() || !scene.isLoaded) break;
                        if (next != null)
                        {
                            var current = next;
                            next = null;
                            if (!visited.Add(current)) { yield return null; continue; }
                            nodes.Push(new NodeCursor(current));
                            renderers.Clear();
                            current.GetComponents(renderers);
                            yield return null;
                            for (var rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
                            {
                                if (!scene.IsValid() || !scene.isLoaded || current == null) break;
                                yield return renderers[rendererIndex];
                            }
                            renderers.Clear();
                        }
                        else
                        {
                            var cursor = nodes.Pop();
                            if (cursor.Node != null)
                            {
                                var childCount = cursor.Node.childCount;
                                if (cursor.NextChild > 0 &&
                                    (childCount != cursor.ObservedChildCount || cursor.NextChild > childCount ||
                                     !ReferenceEquals(cursor.Node.GetChild(cursor.NextChild - 1), cursor.PreviousChild)))
                                {
                                    // A visited child was removed/reparented or the
                                    // list shifted across a yield. Recheck this parent
                                    // from zero; visited subtrees are constant-time skips.
                                    cursor.NextChild = 0;
                                }
                                if (cursor.NextChild < childCount)
                                {
                                    next = cursor.Node.GetChild(cursor.NextChild++);
                                    cursor.PreviousChild = next;
                                    cursor.ObservedChildCount = childCount;
                                    nodes.Push(cursor);
                                }
                            }
                            yield return null;
                        }
                    }
                    nodes.Clear();
                    yield return null;
                }
            }
        }
        finally
        {
            roots.Clear();
            renderers.Clear();
            nodes.Clear();
            visited.Clear();
        }
    }
}
