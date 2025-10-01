/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Generic;
using System.Linq;
using ThingSet.Common;

namespace ThingSet.Server.Common.Nodes;

public class ThingSetFunction<TDelegate> : ThingSetNode, IThingSetParentNode, IThingSetFunction
    where TDelegate : Delegate
{
    private readonly TDelegate _function;

    public ThingSetFunction(ushort id, string name, ushort parentId, TDelegate function) : base(id, name, parentId)
    {
        _function = function;
        Type = ThingSetType.GetType(typeof(TDelegate));
        Children = function.Method
            .GetParameters()
            .Select((p, i) =>
                new ThingSetFunctionParameter(
                    (ushort)(Id + 1 + i),
                    $"w{p.Name}" ?? $"wParam{i + 1}", // TODO: include type
                    Id,
                    ThingSetType.GetType(p.ParameterType)))
            .ToList();
    }

    public Delegate Function => _function;

    public override ThingSetType Type { get; }

    public IEnumerable<ThingSetNode> Children { get; }
}

public static class ThingSetFunction
{
    public static ThingSetFunction<TDelegate> Create<TDelegate>(ushort id, string name, ushort parentId, TDelegate function)
        where TDelegate : Delegate
    {
        return new ThingSetFunction<TDelegate>(id, name, parentId, function);
    }
}