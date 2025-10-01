/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.Threading;
using System.Threading.Tasks;
using SocketCANSharp;
using SocketCANSharp.Network;

namespace ThingSet.Common.Transports.Can;

/// <summary>
/// CAN transport for ThingSet clients. Supports request/response and
/// asynchronous reports.
/// </summary>
public class CanClientTransport : ClientTransportBase<byte>, IClientTransport
{
    private static readonly TimeSpan ExceptionBackoffInterval = TimeSpan.FromSeconds(5);
    private const int ExceptionThreshold = 12;

    private delegate int CanFrameReader(out uint canId, out byte length, out byte[] data);

    private readonly ThingSetCanInterface _canInterface;
    private bool _disposeInterface;

    private readonly IsoTpCanSocket _requestResponseSocket;
    private readonly RawCanSocket _subscriptionSocket;

    private readonly CanFrameReader _canFrameReader;

    private readonly byte _destinationBridge;
    private readonly byte _destinationNodeAddress;

    private readonly CanReportParser _reportParser = new CanReportParser();

    public CanClientTransport(ThingSetCanInterface canInterface, byte destinationNodeAddress, bool leaveOpen) : this(canInterface, CanID.LocalBridge, destinationNodeAddress, leaveOpen)
    {
    }

    public CanClientTransport(ThingSetCanInterface canInterface, byte destinationBridge, byte destinationNodeAddress, bool leaveOpen)
    {
        _canInterface = canInterface;
        _disposeInterface = !leaveOpen;

        _destinationBridge = destinationBridge;
        _destinationNodeAddress = destinationNodeAddress;

        _requestResponseSocket = IsoTpCanSocketFactory.CreateIsoTpCanSocket(canInterface.IsFdMode);
        _subscriptionSocket = new RawCanSocket
        {
            CanFilters = new[]
            {
                new CanFilter((uint)MessagePriority.ReportHigh | (uint)MessageType.SingleFrameReport | CanID.SetSource(destinationNodeAddress), (uint)MessagePriority.ReportHigh | CanID.TypeMask | CanID.SourceMask),
                new CanFilter((uint)MessagePriority.ReportHigh | (uint)MessageType.MultiFrameReport | CanID.SetSource(destinationNodeAddress), (uint)MessagePriority.ReportHigh | CanID.TypeMask | CanID.SourceMask),
            },
            AllCanFiltersMustMatch = false,
            EnableCanFdFrames = canInterface.IsFdMode,
        };

        _canFrameReader = canInterface.IsFdMode ? ReadCanFdFrame : ReadCanFrame;
    }

    protected override string Address => $"{_destinationNodeAddress:x}";

    public override ValueTask ConnectAsync()
    {
        _requestResponseSocket.Bind(_canInterface.Interface,
            txId: CanID.CreateCanID(MessageType.RequestResponse, MessagePriority.Channel, _destinationBridge, _canInterface.NodeAddress, _destinationNodeAddress),
            rxId: CanID.CreateCanID(MessageType.RequestResponse, MessagePriority.Channel, _destinationBridge, _destinationNodeAddress, _canInterface.NodeAddress));
        _requestResponseSocket.SendTimeout = 1000;
        _requestResponseSocket.ReceiveTimeout = 5000;

        return ValueTask.CompletedTask;
    }

    protected override ValueTask SubscribeAsync()
    {
        Console.WriteLine("Binding subscription socket");
        _subscriptionSocket.Bind(_canInterface.Interface);
        return ValueTask.CompletedTask;
    }

    public override bool Write(byte[] buffer, int length)
    {
        int written = LibcNativeMethods.Write(_requestResponseSocket.SafeHandle, buffer, length);
        return written == length;
    }

    public override int Read(byte[] buffer)
    {
        try
        {
            int read = _requestResponseSocket.Read(buffer);
            return read;
        }
        catch (SocketCanException scex)
        {
            throw new TimeoutException("Timed out waiting for data.", scex);
        }
    }

    protected override void Dispose(bool disposing)
    {
        _subscriptionSocket.Dispose();
        _requestResponseSocket.Dispose();
        if (_disposeInterface)
        {
            _canInterface.Dispose();
        }
    }

    private int ReadCanFrame(out uint canId, out byte length, out byte[] data)
    {
        int read = _subscriptionSocket.Read(out CanFrame frame);
        canId = frame.CanId;
        data = frame.Data;
        length = frame.Length;
        return read;
    }

    private int ReadCanFdFrame(out uint canId, out byte length, out byte[] data)
    {
        int read = _subscriptionSocket.Read(out CanFdFrame frame);
        canId = frame.CanId;
        data = frame.Data;
        length = frame.Length;
        return read;
    }

    protected override ValueTask HandleIncomingPublicationsAsync()
    {
        List<SocketCanException> exceptions = new List<SocketCanException>();
        try
        {
            int read = _canFrameReader(out uint canId, out byte length, out byte[] data);
            if (read > 0)
            {
                exceptions.Clear();
                switch (CanID.GetType(canId))
                {
                    case MessageType.SingleFrameReport:
                        NotifyItem(canId, data);
                        break;
                    case MessageType.MultiFrameReport:
                        MultiFrameMessageType type = CanID.GetMultiFrameMessageType(canId);
                        byte sequenceNumber = CanID.GetSequenceNumber(canId);
                        byte messageNumber = CanID.GetMessageNumber(canId);
                        byte source = CanID.GetSource(canId);
                        ReceiveBuffer buffer = _buffersBySender.GetOrAdd(source, _ => new ReceiveBuffer());
                        if (_reportParser.TryParse(sequenceNumber, messageNumber, type, buffer, data, out ulong? eui, out CborReader? reader))
                        {
                            NotifyReport(eui, reader);
                        }
                        break;
                }
            }
        }
        catch (SocketCanException scex)
        {
            exceptions.Add(scex);
            if (exceptions.Count > ExceptionThreshold)
            {
                throw new AggregateException($"Multiple errors occurred while reading from CAN interface {_canInterface.Interface.Name}.", exceptions);
            }
            Thread.Sleep(ExceptionBackoffInterval);
        }
        return ValueTask.CompletedTask;
    }

    private void NotifyItem(uint canId, ReadOnlyMemory<byte> body)
    {
        byte[] buffer = new byte[body.Length + 4];
        buffer[0] = 0xA1; // map with 1 element
        ushort id = CanID.GetDataID(canId);
        int headerLength;
        if (id <= 23)
        {
            buffer[1] = (byte)id;
            headerLength = 2;
        }
        else if (id < 256)
        {
            buffer[1] = 0x18;
            buffer[2] = (byte)id;
            headerLength = 3;
        }
        else
        {
            buffer[1] = 0x19; // ushort
            Span<byte> span = buffer;
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(2), id);
            headerLength = 4;
        }
        ReadOnlyMemory<byte> source = body;
        Memory<byte> memory = buffer;
        source.CopyTo(memory.Slice(headerLength));
        NotifyReport(null, new CborReader(memory));
    }

    private class CanReportParser : ReportParser<MultiFrameMessageType>
    {
        protected override bool IsFirst(MultiFrameMessageType type)
        {
            return type == MultiFrameMessageType.First || type == MultiFrameMessageType.Last;
        }

        protected override bool IsLast(MultiFrameMessageType type)
        {
            return type == MultiFrameMessageType.First || type == MultiFrameMessageType.Last;
        }
    }
}