/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;

namespace ThingSet.Common.Nodes;

public interface IThingSetFunction
{
    public Delegate Function { get; }
}
