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
using ThingSet.Common.Protocols;
using static ThingSet.Common.Transports.Ip.Protocol;

namespace ThingSet.Common.Transports.Ip;

/// <summary>
/// ThingSet server transport for IP (TCP/UDP).
/// </summary>
public class IpServerTransport : ServerTransportBase, IServerTransport
{
    private const int MessageSize = 512;
    private const int HeaderSize = 2;

    private static readonly IPEndPoint Broadcast = new IPEndPoint(IPAddress.Broadcast, Protocol.PublishSubscribePort);

    private readonly TcpListener _listener;
    private readonly UdpClient _udpClient;

    private readonly CancellationTokenSource _listenerCanceller = new CancellationTokenSource();
    private readonly Thread _listenThread;

    private byte _messageNumber;

    private Func<object, Memory<byte>, Memory<byte>>? _callback;

    public IpServerTransport() : this(IPAddress.Any)
    {
    }

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

    public override event EventHandler<ErrorEventArgs>? Error;

    public override ValueTask ListenAsync(Func<object, Memory<byte>, Memory<byte>> callback)
    {
        _callback = callback;
        _listenThread.Start();
        return ValueTask.CompletedTask;
    }

    public override void PublishReport(byte[] buffer)
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
                frame[1] = _messageNumber;
            }

            Span<byte> slice = source.Slice(written, size);
            slice.CopyTo(frame.Slice(HeaderSize));
            _udpClient.Send(frame.Slice(0, HeaderSize + size), Broadcast);
            written += size;
        }
        unchecked
        {
            _messageNumber++;
        }
    }

    protected override void Dispose(bool disposing)
    {
        _listenerCanceller.Cancel();
        _listener.Stop();
        _listener.Dispose();
        _udpClient.Dispose();
    }

    private async void RunListener()
    {
        _listener.Start();

        while (!_listenerCanceller.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_listenerCanceller.Token);
                Task.Run(() => HandleRequest(client, _listenerCanceller.Token)).GetAwaiter();
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task HandleRequest(TcpClient client, CancellationToken cancellationToken)
    {
        Memory<byte> buffer = new byte[8192];
        using (client)
        {
            try
            {
                await using NetworkStream stream = client.GetStream();
                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    Memory<byte> response;
                    try
                    {
                        response = _callback!(client.Client.RemoteEndPoint!, buffer.Slice(0, read));
                    }
                    catch (Exception ex)
                    {
                        Error?.Invoke(this, new ErrorEventArgs(ex));
                        response = new byte[] { (byte)ThingSetStatus.InternalServerError };
                    }
                    await stream.WriteAsync(response, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }
    }
}
