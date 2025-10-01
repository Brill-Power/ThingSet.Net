/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Nodes;

public abstract class ThingSetNode
{
    protected ThingSetNode(ushort id, string name, ushort parentId)
    {
        Id = id;
        ParentId = parentId;
        Name = name;
        ThingSetRegistry.Register(this);
    }

    ~ThingSetNode()
    {
        ThingSetRegistry.Unregister(this);
    }

    public ushort Id { get; }
    public string Name { get; }
    public ushort ParentId { get; }
    public abstract ThingSetType Type { get; }
}
