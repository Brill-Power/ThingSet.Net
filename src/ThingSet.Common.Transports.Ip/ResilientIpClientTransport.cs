/*
 * Copyright (c) 2023-2026 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Net.Sockets;

namespace ThingSet.Common.Transports.Ip;

/// <summary>
/// ThingSet client transport for IP (TCP/UDP).
/// </summary>
public class ResilientIpClientTransport : IpClientTransportBase
{
    public ResilientIpClientTransport(string hostname) : base(hostname)
    {
    }

    public override int Read(byte[] buffer)
    {
        ConnectIfNecessary();
        return _tcpClient.GetStream().Read(buffer, 0, buffer.Length);
    }

    public override bool Write(byte[] buffer, int length)
    {
        ConnectIfNecessary();
        _tcpClient.GetStream().Write(buffer, 0, length);
        return true;
    }

    private void ConnectIfNecessary()
    {
        if (!_tcpClient.Connected)
        {
            try
            {
            _tcpClient.Dispose();
            }
            catch (Exception)
            {
            }
            _tcpClient = new TcpClient();
            _tcpClient.Connect(_hostname, _port);
        }
    }
}
