/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.ObjectModel;
using ThingSet.Common;
using ThingSet.Common.Protocols;

namespace ThingSet.Client.Schema;

/// <summary>
/// Represents an item on a ThingSet device.
/// </summary>
public class ThingSetNode
{
    /// <summary>
    /// Gets the name of the item.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets the numeric ID of the item.
    /// </summary>
    public uint Id { get; init; }
    /// <summary>
    /// Gets the path to the item.
    /// </summary>
    public required string Path { get; init; }
    /// <summary>
    /// Gets the type of the item.
    /// </summary>
    public ThingSetType Type { get; init; }
    /// <summary>
    /// Gets the access controls for this item.
    /// </summary>
    public ThingSetAccess Access { get; init; }
    /// <summary>
    /// Gets the item's children.
    /// </summary>
    public required ReadOnlyCollection<ThingSetNode> Children { get; init; }

    /// <summary>
    /// Gets the fully-qualified name of the item.
    /// </summary>
    public string FullyQualifiedName => (!String.IsNullOrEmpty(Path) ? Path + "/" : String.Empty) + Name;

    public override string ToString() => FullyQualifiedName;
}