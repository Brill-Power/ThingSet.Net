/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Nodes;

public class ThingSetProperty<TValue> : ThingSetNode
{
    public ThingSetProperty(ushort id, string name) : base(id, name)
    {
    }

    public TValue? Value { get; set; }

    public override ThingSetType Type => ThingSetType.GetType(typeof(TValue));
}