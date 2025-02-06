/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;

namespace ThingSet.Common.Transports.Can;

public class AddressEventArgs : EventArgs
{
    public AddressEventArgs(byte nodeAddress)
    {
        NodeAddress = nodeAddress;
    }

    public byte NodeAddress { get; }
    /// <summary>
    /// If set to true, does not add or remove the peer to/from the list of known peers.
    /// </summary>
    public bool Cancel { get; set; }
}