// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Animations.ExpressionsFork;

namespace CompositionExpressions;

/// <summary>
/// Public helpers exposing the typed expression nodes' UI-thread
/// live-value escape hatch (the <c>LiveValueProvider</c>) to consumers
/// outside the CompositionExpressions assembly.
///
/// <para>The <c>LiveValueProvider</c> on each typed node is internal so
/// callers building nodes via <see cref="ExpressionFunctions"/> don't see
/// it as part of the everyday API. But scenarios that pre-bake against an
/// expression tree need to substitute a constant (or sweeping) value for
/// a specific live input while leaving the rest of the tree alone — this
/// is exactly what <see cref="LiveValueOverride"/> provides.</para>
/// </summary>
public static class LiveValueOverride
{
    /// <summary>
    /// Install a UI-thread live-value provider on the given node. While
    /// installed, <see cref="ScalarNode.Evaluate"/> short-circuits and
    /// returns <paramref name="provider"/>'s output instead of walking the
    /// AST. Pass <c>null</c> to remove the override and restore normal
    /// evaluation.
    /// </summary>
    public static void SetLiveValueProvider(this ScalarNode node, Func<float>? provider)
    {
        node.LiveValueProvider = provider!;
    }

    public static void SetLiveValueProvider(this Vector2Node node, Func<Vector2>? provider)
    {
        node.LiveValueProvider = provider!;
    }

    public static void SetLiveValueProvider(this Vector3Node node, Func<Vector3>? provider)
    {
        node.LiveValueProvider = provider!;
    }

    public static void SetLiveValueProvider(this Vector4Node node, Func<Vector4>? provider)
    {
        node.LiveValueProvider = provider!;
    }

    public static void SetLiveValueProvider(this Matrix4x4Node node, Func<Matrix4x4>? provider)
    {
        node.LiveValueProvider = provider!;
    }

    public static void SetLiveValueProvider(this QuaternionNode node, Func<Quaternion>? provider)
    {
        node.LiveValueProvider = provider!;
    }

    /// <summary>
    /// Walks the AST rooted at <paramref name="root"/> and yields every
    /// distinct <see cref="ScalarNode"/> reachable from it. Useful for
    /// auto-discovering the live scalar inputs an expression depends on
    /// without forcing the caller to enumerate them by hand.
    /// </summary>
    public static IEnumerable<ScalarNode> EnumerateScalarLeaves(this ExpressionNode root)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        // Use a hand-rolled reference-identity comparer rather than
        // System.Collections.Generic.ReferenceEqualityComparer so this file
        // compiles against UWP's older BCL too (the type was added in .NET 5).
        var visited = new HashSet<ExpressionNode>(ReferenceComparer.Instance);
        var stack = new Stack<ExpressionNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (!visited.Add(n)) continue;
            if (n is ScalarNode s) yield return s;
            if (n.Children != null)
            {
                foreach (var c in n.Children) stack.Push(c);
            }
        }
    }

    private sealed class ReferenceComparer : IEqualityComparer<ExpressionNode>
    {
        public static readonly ReferenceComparer Instance = new();
        public bool Equals(ExpressionNode? x, ExpressionNode? y) => ReferenceEquals(x, y);
        public int GetHashCode(ExpressionNode obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Walks the AST rooted at <paramref name="root"/> and yields only
    /// <see cref="ScalarNode"/> leaves that look like animatable inputs:
    /// nodes referencing a <see cref="CompositionPropertySet"/> property.
    /// Constants, function compositions, and derived scalars are skipped.
    /// </summary>
    public static IEnumerable<ScalarNode> EnumerateAnimatableScalarLeaves(this ExpressionNode root)
    {
        foreach (var leaf in EnumerateScalarLeaves(root))
        {
            // ReferenceProperty leaves (e.g. propSet.Yaw) are inputs.
            // Other ScalarNodes are operators / constants whose value is
            // fully determined by their children.
            if (leaf.NodeType == ExpressionNodeType.ReferenceProperty)
                yield return leaf;
        }
    }
}
