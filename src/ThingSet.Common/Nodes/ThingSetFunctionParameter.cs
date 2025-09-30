/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Nodes;

public class ThingSetFunctionParameter : ThingSetNode
{
    public ThingSetFunctionParameter(ushort id, string name, ThingSetType type) : base(id, name)
    {
        Type = type;
    }

    public override ThingSetType Type { get; }
}
