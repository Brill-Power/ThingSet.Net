/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace ThingSet.Common.Nodes;

public class ThingSetFunction<TDelegate> : ThingSetParentNode
    where TDelegate : Delegate
{
    private readonly TDelegate _function;

    public ThingSetFunction(ushort id, string name, TDelegate function) : base(id, name, GetParameters(function, id + 1))
    {
        _function = function;
        Type = ThingSetType.GetType(typeof(TDelegate));
    }

    public override ThingSetType Type { get; }

    private static IEnumerable<ThingSetFunctionParameter> GetParameters(TDelegate function, int startingId)
    {
        return function.Method
            .GetParameters()
            .Select((p, i) =>
                new ThingSetFunctionParameter(
                    (ushort)(startingId + i),
                    $"w{p.Name}" ?? $"wParam{i + 1}", // TODO: include type
                    ThingSetType.GetType(p.ParameterType)));
    }
}

public static class ThingSetFunction
{
    public static ThingSetFunction<TDelegate> Create<TDelegate>(ushort id, string name, TDelegate function)
        where TDelegate : Delegate
    {
        return new ThingSetFunction<TDelegate>(id, name, function);
    }
}