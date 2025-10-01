/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using ThingSet.Common;

namespace ThingSet.Server.Common.Nodes;

public class ThingSetFunctionParameter : ThingSetNode
{
    public ThingSetFunctionParameter(ushort id, string name, ushort parentId, ThingSetType type) : base(id, name, parentId)
    {
        Type = type;
    }

    public override ThingSetType Type { get; }
}
