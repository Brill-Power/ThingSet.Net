/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System.Formats.Cbor;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static ThingSet.Common.Transports.Ip.Protocol;

namespace ThingSet.Common.Transports.Ip;

/// <summary>
/// ThingSet client transport for IP (TCP/UDP).
/// </summary>
public class IpClientTransport : ClientTransportBase<IPEndPoint>, IClientTransport
{
    private readonly string _hostname;
    private readonly int _port;

    private readonly TcpClient _tcpClient;
    private readonly UdpClient _udpClient;

    private readonly UdpReportParser _reportParser;

    public IpClientTransport(string hostname, ILogger? logger = null) : this(hostname, Protocol.RequestResponsePort, logger)
    {
    }

    public IpClientTransport(string hostname, int port, ILogger? logger = null) : base(logger: logger)
    {
        _reportParser = new UdpReportParser(logger);

        _hostname = hostname;
        _port = port;
        _tcpClient = new TcpClient();
        _udpClient = new UdpClient();
    }

    public override string PeerAddress => _hostname;

    public async override ValueTask ConnectAsync()
    {
        await _tcpClient.ConnectAsync(_hostname, _port);
    }

    public override int Read(byte[] buffer)
    {
        return _tcpClient.GetStream().Read(buffer, 0, buffer.Length);
    }

    protected override ValueTask SubscribeAsync()
    {
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, Protocol.PublishSubscribePort));
        return ValueTask.CompletedTask;
    }

    public override bool Write(byte[] buffer, int length)
    {
        _tcpClient.GetStream().Write(buffer, 0, length);
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        _tcpClient.Dispose();
        _udpClient.Dispose();
    }

    protected async override ValueTask HandleIncomingPublicationsAsync()
    {
        try
        {
            UdpReceiveResult result = await _udpClient.ReceiveAsync();
            MessageType messageType = (MessageType)(result.Buffer[0] & 0xF0);
            byte sequenceNumber = (byte)(result.Buffer[0] & 0x0F);
            byte messageNumber = result.Buffer[1];
            ReceiveBuffer buffer = GetOrCreateBuffer(result.RemoteEndPoint);
            if (_reportParser.TryParse(sequenceNumber, messageNumber, messageType, buffer, result.Buffer, out ulong? eui, out CborReader? reader))
            {
                NotifyReport(eui, reader);
            }
        }
        catch (SocketException)
        {
        }
    }

    private class UdpReportParser : ReportParser<MessageType>
    {
        public UdpReportParser(ILogger? logger) : base(logger)
        {
        }

        protected override int Offset => 2;

        protected override bool IsFirst(MessageType type)
        {
            return type == MessageType.Single || type == MessageType.First;
        }

        protected override bool IsLast(MessageType type)
        {
            return type == MessageType.Single || type == MessageType.Last;
        }
    }
}
