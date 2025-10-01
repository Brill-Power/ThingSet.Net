/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Nodes;

public class ThingSetGroup : ThingSetParentNode
{
    public ThingSetGroup(ushort id, string name, ushort parentId) : base(id, name, parentId)
    {
    }

    public override ThingSetType Type => ThingSetType.Group;
}
