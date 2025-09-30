/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ThingSet.Common.Nodes;

public abstract class ThingSetNode
{
    protected ThingSetNode(ushort id, string name)
    {
        Id = id;
        Name = name;
        ThingSetRegistry.Register(this);
    }

    ~ThingSetNode()
    {
        ThingSetRegistry.Unregister(this);
    }

    public ushort Id { get; }
    public string Name { get; }
    public abstract ThingSetType Type{ get; }
}

public class ThingSetRegistry
{
    private static readonly ThingSetRegistry Instance = new ThingSetRegistry();

    private readonly IDictionary<ushort, ThingSetNode> _nodesById = new ConcurrentDictionary<ushort, ThingSetNode>();

    private ThingSetRegistry()
    {
    }

    public static bool TryGetNode(ushort id, [NotNullWhen(true)] out ThingSetNode? node)
    {
        return Instance._nodesById.TryGetValue(id, out node);
    }

    internal static void Register(ThingSetNode node)
    {
        Instance._nodesById.Add(node.Id, node);
    }

    internal static void Unregister(ThingSetNode node)
    {
        Instance._nodesById.Remove(node.Id);
    }
}