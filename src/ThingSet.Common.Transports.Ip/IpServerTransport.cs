/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static ThingSet.Common.Transports.Ip.Protocol;

namespace ThingSet.Common.Transports.Ip;

/// <summary>
/// ThingSet server transport for IP (TCP/UDP).
/// </summary>
public class IpServerTransport : IServerTransport
{
    private const int MessageSize = 512;
    private const int HeaderSize = 2;

    private static readonly IPEndPoint Broadcast = new IPEndPoint(IPAddress.Broadcast, Protocol.PublishSubscribePort);

    private readonly TcpListener _listener;
    private readonly UdpClient _udpClient;

    private readonly Thread _listenThread;
    private bool _runListener = true;

    private byte _messageNumber;

    private Func<object, Memory<byte>, Memory<byte>>? _callback;

    public IpServerTransport() : this(IPAddress.Any)
    {
    }

    public event EventHandler<ErrorEventArgs>? Error;

    public IpServerTransport(IPAddress listenAddress)
    {
        _listener = new TcpListener(listenAddress, Protocol.RequestResponsePort);
        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        _udpClient = new UdpClient(Protocol.PublishSubscribePort, AddressFamily.InterNetwork);
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        _listenThread = new Thread(RunListener)
        {
            IsBackground = true,
        };
    }

    public void Dispose()
    {
        _listener.Dispose();
        _udpClient.Dispose();
    }

    public ValueTask ListenAsync(Func<object, Memory<byte>, Memory<byte>> callback)
    {
        _callback = callback;
        _listenThread.Start();
        return ValueTask.CompletedTask;
    }

    public void PublishControl(ushort id, byte[] buffer)
    {
        throw new NotImplementedException();
    }

    public void PublishReport(byte[] buffer)
    {
        int written = 0;
        MessageType messageType;
        Span<byte> source = buffer;
        Span<byte> frame = stackalloc byte[MessageSize + HeaderSize];
        byte sequenceNumber = 0;
        while (written < buffer.Length)
        {
            int size = Math.Min(MessageSize, buffer.Length - written);
            messageType = written == 0 ?
                (buffer.Length < MessageSize ?
                    MessageType.Single : MessageType.First) :
                (buffer.Length - written <= MessageSize) ?
                    MessageType.Last : MessageType.Consecutive;
            frame[0] = (byte)((byte)messageType | (sequenceNumber++ & 0x0F));
            unchecked
            {
                frame[1] = _messageNumber++;
            }

            Span<byte> slice = source.Slice(written, size);
            slice.CopyTo(frame.Slice(HeaderSize));
            _udpClient.Send(frame.Slice(0, HeaderSize + size), Broadcast);
            written += size;
        }
    }

    private async void RunListener()
    {
        _listener.Start();

        while (_runListener)
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            Task.Run(() => HandleRequest(client)).GetAwaiter();
        }
    }

    private async Task HandleRequest(TcpClient client)
    {
        Memory<byte> buffer = new byte[8192];
        using (client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                int read = await stream.ReadAsync(buffer);
                Memory<byte> response = _callback!(client.Client.RemoteEndPoint!, buffer.Slice(0, read));
                await stream.WriteAsync(response);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }
    }
}
