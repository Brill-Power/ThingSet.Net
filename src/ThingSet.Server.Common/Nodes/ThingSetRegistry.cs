/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Json.Pointer;
using ThingSet.Common;
using ThingSet.Common.Protocols;

namespace ThingSet.Server.Common.Nodes;

public class ThingSetRegistry
{
    private static readonly ThingSetRegistry Instance = new ThingSetRegistry();

    public static readonly ThingSetParentNode Root = new RootNode();
    public static readonly ThingSetNode Metadata = new OverlayNode(0x19, "_Metadata");

    private readonly IDictionary<ushort, ThingSetNode> _nodesById = new ConcurrentDictionary<ushort, ThingSetNode>();

    private ThingSetRegistry()
    {
    }

    public static bool TryGetNode(ushort id, [NotNullWhen(true)] out ThingSetNode? node)
    {
        return Instance._nodesById.TryGetValue(id, out node);
    }

    public static bool TryGetNode(string path, [NotNullWhen(true)] out ThingSetNode? node, [NotNullWhen(false)] out ThingSetStatus? error)
    {
        if (String.IsNullOrEmpty(path))
        {
            // root object
            node = Root;
            error = null;
            return true;
        }

        if (path[0] == '/')
        {
            // what if a gateway?
            node = null;
            error = ThingSetStatus.NotAGateway;
            return false;
        }

        if (!JsonPointer.TryParse("/" + path, out JsonPointer pointer))
        {
            node = null;
            error = ThingSetStatus.BadRequest;
            return false;
        }

        node = Root;
        //foreach (string segment in pointer)
        for (int i = 0; i < pointer.SegmentCount; i++)
        {
            JsonPointerSegment segment = pointer.GetSegment(i);
            if (node is IThingSetParentNode parent)
            {
                foreach (ThingSetNode child in parent.Children)
                {
                    if (segment.AsSpan().Equals(child.Name, StringComparison.InvariantCulture))
                    {
                        node = child;
                        break;
                    }
                }
            }
        }
        if (node == Root)
        {
            error = ThingSetStatus.NotFound;
            return false;
        }

        error = null;
        return true;
    }

    internal static void Register(ThingSetNode node)
    {
        Instance._nodesById.Add(node.Id, node);
    }

    internal static void Unregister(ThingSetNode node)
    {
        Instance._nodesById.Remove(node.Id);
    }

    internal static IEnumerable<ThingSetNode> GetChildren(ushort parentId)
    {
        return Instance._nodesById.Values.Where(n => n.ParentId == parentId && n.Id != parentId); // double check for Root node
    }

    private class RootNode : ThingSetParentNode
    {
        public RootNode() : base(0x0, String.Empty, 0x0)
        {
        }

        public override ThingSetType Type => ThingSetType.Group;
    }

    private class OverlayNode : ThingSetNode
    {
        public OverlayNode(ushort id, string name) : base(id, name, 0x0)
        {
        }

        public override ThingSetType Type => "overlay";
    }
}