/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System.Collections.Generic;
using System.Linq;

namespace ThingSet.Common.Nodes;

public abstract class ThingSetParentNode : ThingSetNode
{
    protected ThingSetParentNode(ushort id, string name, IEnumerable<ThingSetNode> children) : this(id, name, children.ToList())
    {
    }

    protected ThingSetParentNode(ushort id, string name, IReadOnlyCollection<ThingSetNode> children) : base(id, name)
    {
        Children = children;
    }

    public IReadOnlyCollection<ThingSetNode> Children { get; }
}
