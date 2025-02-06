/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Transports.Can;

public class AddressClaimEventArgs : AddressEventArgs
{
    public AddressClaimEventArgs(byte nodeAddress, byte bridgeId, ulong nodeId)
        : base(nodeAddress)
    {
        BridgeId = bridgeId;
        NodeId = nodeId;
    }

    public byte BridgeId { get; }
    public ulong NodeId { get; }
}