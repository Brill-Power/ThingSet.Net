/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;

namespace ThingSet.Client.Schema;

/// <summary>
/// Controls the operation of the <see cref="ThingSetClient"/> GetNodes method.
/// </summary>
[Flags]
public enum ThingSetNodeEnumerationOptions
{
    TopLevelOnly = 1,
    TopLevelAndChildren = 2,
    LeafNodesOnly = 4,
    WriteableOnly = 8,
    ExecutableOnly = 16,
    IncludeHidden = 32,
    IncludeExecutable = 64,
    IncludeWriteable = 128,
    IncludeRecordChildren = 256,
    All = TopLevelAndChildren | IncludeHidden | IncludeExecutable | IncludeWriteable | IncludeRecordChildren,
}