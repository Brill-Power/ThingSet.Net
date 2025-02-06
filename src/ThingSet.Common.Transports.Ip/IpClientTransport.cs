/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Buffers.Binary;
using System.Formats.Cbor;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ThingSet.Common.Protocols.Binary;

namespace ThingSet.Common.Transports.Ip;

public class IpClientTransport : IClientTransport
{
    private readonly string _hostname;
    private readonly int _port;

    private readonly TcpClient _tcpClient;
    private readonly UdpClient _udpClient;

    private readonly Thread _subscriptionThread;
    private bool _runSubscriptionThread = true;

    private Action<ReadOnlyMemory<byte>>? _callback;

    public IpClientTransport(string hostname, int port)
    {
        _hostname = hostname;
        _port = port;
        _tcpClient = new TcpClient();
        _udpClient = new UdpClient();

        _subscriptionThread = new Thread(RunSubscriptionThread)
        {
            IsBackground = true,
            Name = $"Subscription {hostname:x}",
        };
    }

    public async ValueTask ConnectAsync()
    {
        await _tcpClient.ConnectAsync(_hostname, _port);
    }

    public void Dispose()
    {
        _runSubscriptionThread = false;
        if (_subscriptionThread.IsAlive)
        {
            _subscriptionThread.Join(1000);
        }
        _tcpClient.Dispose();
        _udpClient.Dispose();
    }

    public int Read(byte[] buffer)
    {
        return _tcpClient.GetStream().Read(buffer, 0, buffer.Length);
    }

    public ValueTask SubscribeAsync(Action<ReadOnlyMemory<byte>> callback)
    {
        _callback = callback;
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 9002));
        _subscriptionThread.Start();
        return ValueTask.CompletedTask;
    }

    public bool Write(byte[] buffer, int length)
    {
        _tcpClient.GetStream().Write(buffer, 0, length);
        return true;
    }

    private async void RunSubscriptionThread()
    {
        while (_runSubscriptionThread)
        {
            UdpReceiveResult result = await _udpClient.ReceiveAsync();
            if (result.Buffer[0] == (byte)ThingSetRequest.Report && result.Buffer.Length > 4)
            {
                ushort len = (ushort)(result.Buffer[1] | (result.Buffer[2] << 8));
                if (len == result.Buffer.Length - 3)
                {
                    ReadOnlyMemory<byte> memory = result.Buffer;
                    NotifyReport(memory.Slice(3));
                }
            }
        }
    }

    private void NotifyReport(ReadOnlyMemory<byte> body)
    {
        CborReader reader = new CborReader(body, CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
        reader.ReadUInt32();
        _callback?.Invoke(body.Slice(body.Length - reader.BytesRemaining));
    }
}
