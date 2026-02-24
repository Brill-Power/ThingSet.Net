/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Transports.Ip;

/// <summary>
/// ThingSet client transport for IP (TCP/UDP).
/// </summary>
public class IpClientTransport : IpClientTransportBase
{
    public IpClientTransport(string hostname) : base(hostname)
    {
    }

    public IpClientTransport(string hostname, int port) : base(hostname, port)
    {
    }
}
