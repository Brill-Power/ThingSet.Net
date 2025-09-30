/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System.Collections.Generic;

namespace ThingSet.Common.Nodes;

public class ThingSetGroup : ThingSetParentNode
{
    public ThingSetGroup(ushort id, string name, IEnumerable<ThingSetNode> children) : base(id, name, children)
    {
    }

    public ThingSetGroup(ushort id, string name, params ThingSetNode[] children) : base(id, name, children)
    {
    }

    public override ThingSetType Type => ThingSetType.Group;
}
