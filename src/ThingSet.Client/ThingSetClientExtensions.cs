/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ThingSet.Client;

public static class ThingSetClientExtensions
{
    private const uint NodeID = 0x1d;

    public static bool TryGetNodeId(this IThingSetClient thingSetClient, [NotNullWhen(true)] out ulong? nodeId)
    {
        object? result;
        try
        {
            result = thingSetClient.Get(NodeID);
        }
        catch (TimeoutException)
        {
            nodeId = null;
            return false;
        }
        catch (ThingSetException tsex) when (tsex.ErrorCode == Common.Protocols.ThingSetStatus.NotFound)
        {
            nodeId = null;
            return false;
        }
        catch (ThingSetException tsex) when (tsex.ErrorCode == Common.Protocols.ThingSetStatus.MethodNotAllowed)
        {
            nodeId = null;
            return false;
        }
        if (result is string id &&
            UInt64.TryParse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong t))
        {
            nodeId = t;
            return true;
        }

        nodeId = null;
        return false;
    }
}