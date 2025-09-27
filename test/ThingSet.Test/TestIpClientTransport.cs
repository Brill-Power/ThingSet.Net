/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using ThingSet.Common.Transports.Ip;

namespace ThingSet.Test;

public class TestIpClientTransport
{
    [Test]
    public async Task TestDisposal()
    {
        using (TcpClient server = new TcpClient(new IPEndPoint(IPAddress.Loopback, 9001)))
        {
            server.Client.Listen();
            IpClientTransport transport = new IpClientTransport("127.0.0.1", 9001);
            await transport.ConnectAsync();
            await transport.SubscribeAsync(delegate { });
            await Task.Delay(100);
            Assert.DoesNotThrow(() => transport.Dispose());
        }
    }
}
