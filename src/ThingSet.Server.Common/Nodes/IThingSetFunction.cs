/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;

namespace ThingSet.Server.Common.Nodes;

public interface IThingSetFunction
{
    public Delegate Function { get; }
}
