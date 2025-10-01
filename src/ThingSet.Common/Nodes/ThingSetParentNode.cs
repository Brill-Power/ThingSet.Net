/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System.Collections.Generic;

namespace ThingSet.Common.Nodes;

public abstract class ThingSetParentNode : ThingSetNode, IThingSetParentNode
{
    protected ThingSetParentNode(ushort id, string name, ushort parentId) : base(id, name, parentId)
    {
    }

    public IEnumerable<ThingSetNode> Children => ThingSetRegistry.GetChildren(ParentId);
}
