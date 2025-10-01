/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Nodes;

public class ThingSetProperty<TValue> : ThingSetNode, IThingSetValue
{
    public ThingSetProperty(ushort id, string name, ushort parentId) : base(id, name, parentId)
    {
    }

    public TValue? Value { get; set; }

    public override ThingSetType Type => ThingSetType.GetType(typeof(TValue));

    object? IThingSetValue.Value
    {
        get { return Value; }
        set { Value = (TValue?)value; }
    }
}
