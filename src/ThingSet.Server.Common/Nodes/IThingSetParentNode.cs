/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System.Collections.Generic;

namespace ThingSet.Server.Common.Nodes;

public interface IThingSetParentNode
{
    IEnumerable<ThingSetNode> Children { get;  }
}
