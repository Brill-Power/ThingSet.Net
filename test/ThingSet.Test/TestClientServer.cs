/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System.Net;
using System.Threading.Tasks;
using ThingSet.Client;
using ThingSet.Common.Nodes;
using ThingSet.Common.Transports.Ip;
using ThingSet.Server;

namespace ThingSet.Test;

public class TestClientServer
{
    [Test]
    public async Task TestProperty()
    {
        ThingSetProperty<float> voltage = new ThingSetProperty<float>(0x200, "voltage");
        voltage.Value = 24.0f;

        using (IpClientTransport clientTransport = new IpClientTransport("127.0.0.1"))
        using (IpServerTransport serverTransport = new IpServerTransport(IPAddress.Loopback))
        using (ThingSetServer server = new ThingSetServer(serverTransport))
        using (ThingSetClient client = new ThingSetClient(clientTransport))
        {
            await server.ListenAsync();
            await Task.Delay(10);
            await client.ConnectAsync();
            object? result = client.Get(0x200);
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<float>());
            Assert.That(result, Is.EqualTo(24.0f));
        }
    }

    [Test]
    public async Task TestFunction()
    {
        var function = ThingSetFunction.Create(0x500, "xTest", (int x, int y) => x + y);

        using (IpClientTransport clientTransport = new IpClientTransport("127.0.0.1"))
        using (IpServerTransport serverTransport = new IpServerTransport(IPAddress.Loopback))
        using (ThingSetServer server = new ThingSetServer(serverTransport))
        using (ThingSetClient client = new ThingSetClient(clientTransport))
        {
            await server.ListenAsync();
            await Task.Delay(10);
            await client.ConnectAsync();
            object? result = client.Fetch(0x500);
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<object[]>());
            if (result is object[] array)
            {
                Assert.That(array.Length, Is.EqualTo(2));
                Assert.That(array[0], Is.EqualTo(0x501));
                Assert.That(array[1], Is.EqualTo(0x502));
            }

            result = client.Exec(0x500, 1, 2);
            Assert.That(result, Is.Not.Null);
        }
    }
}