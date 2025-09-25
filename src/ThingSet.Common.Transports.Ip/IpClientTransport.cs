/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Concurrent;
using System.Formats.Cbor;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ThingSet.Common.Protocols.Binary;

namespace ThingSet.Common.Transports.Ip;

public class IpClientTransport : IClientTransport
{
    private const int MessageSize = 512;
    private const int MessageTypePosition = 4;

    private readonly string _hostname;
    private readonly int _port;

    private readonly TcpClient _tcpClient;
    private readonly UdpClient _udpClient;

    private readonly ConcurrentDictionary<IPEndPoint, ReceiveBuffer> _buffersBySender = new ConcurrentDictionary<IPEndPoint, ReceiveBuffer>();
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
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
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
            MessageType messageType = (MessageType)(result.Buffer[0] & 0xF0);
            byte sequenceNumber = (byte)(result.Buffer[0] & 0x0F);
            byte messageNumber = result.Buffer[1];
            ReceiveBuffer buffer = _buffersBySender.GetOrAdd(result.RemoteEndPoint, _ => new ReceiveBuffer());
            if (messageType == MessageType.Single || messageType == MessageType.First)
            {
                buffer.Started = true;
                buffer.MessageNumber = messageNumber;
            }
            else if (buffer.MessageNumber != messageNumber)
            {
                buffer.Reset();
                continue;
            }
            else if (!buffer.Started)
            {
                buffer.Reset();
                continue;
            }

            if (sequenceNumber == (buffer.Sequence++ & 0xF))
            {
                buffer.Append(result.Buffer);
            }
            if ((messageType == MessageType.Last || messageType == MessageType.Single) &&
                buffer.Buffer[0] == (byte)ThingSetRequest.Report)
            {
                ReadOnlyMemory<byte> memory = buffer.Buffer;
                var len = memory.Length;
                NotifyReport(memory.Slice(1, buffer.Position - 1));
                buffer.Reset();
            }
        }
    }

    private void NotifyReport(ReadOnlyMemory<byte> body)
    {
        CborReader reader = new CborReader(body, CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
        reader.ReadUInt32();
        _callback?.Invoke(body.Slice(body.Length - reader.BytesRemaining));
    }

    private class ReceiveBuffer
    {
        public byte[] Buffer = new byte[32768];
        public int Position;
        public byte Sequence;
        public byte MessageNumber;
        public bool Started;

        public void Reset()
        {
            Position = 0;
            Sequence = 0;
            Started = false;
        }

        public void Append(byte[] buffer)
        {
            ReadOnlySpan<byte> source = buffer;
            Span<byte> destination = Buffer;
            source.Slice(2).CopyTo(destination.Slice(Position));
            Position += buffer.Length - 2;
        }
    }

    private enum MessageType
    {
        First = 0x0 << MessageTypePosition,
        Consecutive = 0x1 << MessageTypePosition,
        Last = 0x2 << MessageTypePosition,
        Single = 0x3 << MessageTypePosition,
    }
}
