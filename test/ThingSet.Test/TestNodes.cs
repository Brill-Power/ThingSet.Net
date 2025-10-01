/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Linq;
using ThingSet.Server.Common.Nodes;

namespace ThingSet.Test;

public class TestNodes
{
    [Test]
    public void TestFunctions()
    {
        Func<int, int, int> add = (x, y) => x + y;
        var xAdd = ThingSetFunction.Create(0x1000, "xAdd", 0x0, add);
        Assert.That(xAdd.Type.Type, Is.EqualTo("(i32,i32)->(i32)"));
        Assert.That(xAdd.Children.Count(), Is.EqualTo(2));
    }
}