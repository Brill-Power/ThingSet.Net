/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ThingSet.Common.Protocols;

namespace ThingSet.Client.Schema;

public class ThingSetSchema : IEnumerable<ThingSetNode>
{
    private static class OverlayEndpoints
    {
        public const uint IDs = 0x16;
        public const uint Paths = 0x17;
        public const uint Metadata = 0x19;
        public const uint MetadataName = 0x1A;
        public const uint MetadataType = 0x1B;
        public const uint MetadataAccess = 0x1C;
    }

    public static readonly ThingSetSchema Empty = new ThingSetSchema();

    private readonly ReadOnlyCollection<ThingSetNode> _schema;
    private readonly Dictionary<string, ThingSetNode> _allPropertiesByName = new Dictionary<string, ThingSetNode>();
    private readonly Dictionary<uint, ThingSetNode> _allPropertiesById = new Dictionary<uint, ThingSetNode>();

    private ThingSetSchema()
    {
        _schema = new List<ThingSetNode>().AsReadOnly();
    }

    public ThingSetSchema(ThingSetClient client)
    {
        List<ThingSetNode> props = GetSchema(client, 0, String.Empty, _allPropertiesByName);
        foreach (var pair in _allPropertiesByName)
        {
            _allPropertiesById[pair.Value.Id] = pair.Value;
        }
        _schema = props.AsReadOnly();
    }

    public bool IsEmpty => _schema.Count == 0;

    public bool TryGetNode(uint id, out ThingSetNode? property)
    {
        return _allPropertiesById.TryGetValue(id, out property);
    }

    public IEnumerable<ThingSetNode> GetNodes(ThingSetNodeEnumerationOptions options)
    {
        return GetNodes(_schema, options);
    }

    private IEnumerable<ThingSetNode> GetNodes(IEnumerable<ThingSetNode> nodes, ThingSetNodeEnumerationOptions options)
    {
        foreach (ThingSetNode node in nodes)
        {
            if (node.Name[0] == '_' && (options & ThingSetNodeEnumerationOptions.IncludeHidden) != ThingSetNodeEnumerationOptions.IncludeHidden)
            {
                continue;
            }
            if (node.Name[0] == 'x' && (options & ThingSetNodeEnumerationOptions.IncludeExecutable) != ThingSetNodeEnumerationOptions.IncludeExecutable)
            {
                continue;
            }
            if ((node.Name[0] == 'p' || node.Name[0] == 's') && (options & ThingSetNodeEnumerationOptions.IncludeWriteable) != ThingSetNodeEnumerationOptions.IncludeWriteable)
            {
                continue;
            }
            if (node.Name[0] == 'r' && (options & ThingSetNodeEnumerationOptions.WriteableOnly) == ThingSetNodeEnumerationOptions.WriteableOnly)
            {
                continue;
            }
            if (node.Children is null || node.Children.Count == 0 || node.Type == ThingSetType.Record ||
                (options & ThingSetNodeEnumerationOptions.LeafNodesOnly) != ThingSetNodeEnumerationOptions.LeafNodesOnly)
            {
                yield return node;
            }
            if (node.Children is not null && node.Children.Count > 0 &&
                (node.Type != ThingSetType.Record || (options & ThingSetNodeEnumerationOptions.IncludeRecordChildren) == ThingSetNodeEnumerationOptions.IncludeRecordChildren) &&
                (options & ThingSetNodeEnumerationOptions.TopLevelAndChildren) == ThingSetNodeEnumerationOptions.TopLevelAndChildren)
            {
                foreach (ThingSetNode child in GetNodes(node.Children, options | ThingSetNodeEnumerationOptions.TopLevelAndChildren))
                {
                    yield return child;
                }
            }
        }
    }

    private List<ThingSetNode> GetSchema(ThingSetClient client, uint id, string path, Dictionary<string, ThingSetNode> schema)
    {
        List<ThingSetNode> nodes = new List<ThingSetNode>();
        try
        {
            string prefix = String.IsNullOrEmpty(path) ? String.Empty : path + "/";
            if (client.Fetch(id) is object[] ids && client.Fetch(path) is object[] names &&
                ids.Length > 0 && client.Fetch(OverlayEndpoints.Metadata, ids) is object[] metadata)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    string childName = (string)names[i];
                    uint childId = (uint)ids[i];
                    List<ThingSetNode> children;
                    if (!TryGetType(metadata[i], out ThingSetType type))
                    {
                        type = String.Empty;
                    }
                    ThingSetAccess access = GetAccess(metadata[i]);

                    if (type == ThingSetType.Group || type.IsRecord || type.IsFunction)
                    {
                        children = GetSchema(client, childId, prefix + childName, schema);
                    }
                    else
                    {
                        children = new List<ThingSetNode>();
                    }
                    ThingSetNode node = new ThingSetNode
                    {
                        Name = childName,
                        Id = childId,
                        Path = path,
                        Children = children.AsReadOnly(),
                        Type = type,
                        Access = access,
                    };
                    nodes.Add(node);
                    schema[prefix + childName] = node;
                }
            }
        }
        catch (ThingSetException tsex) when (tsex.ErrorCode == ThingSetStatus.Unauthorised)
        {
        }
        return nodes;
    }

    private static ThingSetAccess GetAccess(object? metadatum)
    {
        if (metadatum is Dictionary<object, object> map &&
            map.TryGetValue(OverlayEndpoints.MetadataAccess, out object? t) &&
            t is uint u)
        {
            return (ThingSetAccess)u;
        }

        return ThingSetAccess.AnyReadWrite;
    }

    private static bool TryGetType(object? metadatum, out ThingSetType type)
    {
        if (metadatum is Dictionary<object, object> map &&
            (map.TryGetValue("type", out object? t) || map.TryGetValue(OverlayEndpoints.MetadataType, out t))
            && t is string s)
        {
            type = s;
            return true;
        }

        type = default;
        return false;
    }

    public IEnumerator<ThingSetNode> GetEnumerator()
    {
        return _schema.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}