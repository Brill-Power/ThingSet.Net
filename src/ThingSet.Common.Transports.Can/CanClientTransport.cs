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
using ThingSet.Common.Protocols.Binary;

namespace ThingSet.Common.Transports.Can;

/// <summary>
/// CAN transport for ThingSet clients. Supports request/response and
/// asynchronous reports.
/// </summary>
public class CanClientTransport : CanTransportBase, IClientTransport
{
    private static readonly TimeSpan ExceptionBackoffInterval = TimeSpan.FromSeconds(5);
    private const int ExceptionThreshold = 12;

    private delegate int CanFrameReader(out uint canId, out byte length, out byte[] data);

    private readonly IsoTpCanSocket _requestResponseSocket;
    private readonly RawCanSocket _subscriptionSocket;

    private readonly CanFrameReader _canFrameReader;

    private readonly byte _destinationBridge;
    private readonly byte _destinationNodeAddress;

    private readonly Dictionary<byte, ReceiveBuffer> _buffersByNodeAddress = new Dictionary<byte, ReceiveBuffer>();

    private readonly Thread _subscriptionThread;
    private bool _runSubscriptionThread = true;

    private Action<ReadOnlyMemory<byte>>? _callback;

    public CanClientTransport(ThingSetCanInterface canInterface, byte destinationNodeAddress, bool leaveOpen) : this(canInterface, CanID.LocalBridge, destinationNodeAddress, leaveOpen)
    {
    }

    public CanClientTransport(ThingSetCanInterface canInterface, byte destinationBridge, byte destinationNodeAddress, bool leaveOpen) : base(canInterface, leaveOpen)
    {
        _destinationBridge = destinationBridge;
        _destinationNodeAddress = destinationNodeAddress;

        _requestResponseSocket = CreateIsoTpCanSocket(canInterface.IsFdMode);
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

        _subscriptionThread = new Thread(RunSubscriptionThread)
        {
            IsBackground = true,
            Name = $"Subscription {_destinationNodeAddress:x}",
        };
    }

    public ValueTask ConnectAsync()
    {
        _requestResponseSocket.Bind(_canInterface.Interface,
            txId: CanID.CreateCanID(MessageType.RequestResponse, MessagePriority.Channel, _destinationBridge, _canInterface.NodeAddress, _destinationNodeAddress),
            rxId: CanID.CreateCanID(MessageType.RequestResponse, MessagePriority.Channel, _destinationBridge, _destinationNodeAddress, _canInterface.NodeAddress));
        _requestResponseSocket.SendTimeout = 1000;
        _requestResponseSocket.ReceiveTimeout = 5000;

        return ValueTask.CompletedTask;
    }

    public ValueTask SubscribeAsync(Action<ReadOnlyMemory<byte>> callback)
    {
        if (_callback is not null)
        {
            throw new InvalidOperationException("There is already a subscription established.");
        }

        _callback = callback;

        Console.WriteLine("Binding subscription socket");
        _subscriptionSocket.Bind(_canInterface.Interface);
        _subscriptionThread.Start();

        return ValueTask.CompletedTask;
    }

    public bool Write(byte[] buffer, int length)
    {
        int written = LibcNativeMethods.Write(_requestResponseSocket.SafeHandle, buffer, length);
        return written == length;
    }

    public int Read(byte[] buffer)
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
        base.Dispose(disposing);
        _runSubscriptionThread = false;
        if (_subscriptionThread.IsAlive)
        {
            _subscriptionThread.Join(1000);
        }
        _subscriptionSocket.Dispose();
        _requestResponseSocket.Dispose();
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

    private void RunSubscriptionThread()
    {
        while (_runSubscriptionThread)
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
                            byte source = CanID.GetSource(canId);
                            byte messageNumber = CanID.GetMessageNumber(canId);
                            byte sequence = CanID.GetSequenceNumber(canId);
                            if (!_buffersByNodeAddress.TryGetValue(source, out ReceiveBuffer? buffer))
                            {
                                _buffersByNodeAddress[source] = buffer = new ReceiveBuffer();
                            }
                            MultiFrameMessageType type = CanID.GetMultiFrameMessageType(canId);
                            if (type == MultiFrameMessageType.Single || type == MultiFrameMessageType.First)
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

                            if (sequence == (buffer.Sequence++ & 0xf))
                            {
                                ReadOnlySpan<byte> span = data;
                                Span<byte> target = buffer.Buffer;
                                span.CopyTo(target.Slice(buffer.Position));
                                buffer.Position += length;
                                if (type == MultiFrameMessageType.Single || type == MultiFrameMessageType.Last)
                                {
                                    ReadOnlyMemory<byte> memory = buffer.Buffer;
                                    if (buffer.Buffer[0] == (byte)ThingSetRequest.Report)
                                    {
                                        NotifyReport(memory.Slice(1, buffer.Position - 1));
                                    }
                                    buffer.Reset();
                                }
                            }
                            else
                            {
                                // invalid sequence; reset
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
                    throw new AggregateException($"Multiple errors occurred while reading from CAN interface {_canInterface.Interface.Name}.");
                }
                Thread.Sleep(ExceptionBackoffInterval);
            }
        }
    }

    private void NotifyReport(ReadOnlyMemory<byte> body)
    {
        CborReader reader = new CborReader(body, CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
        reader.ReadUInt32();
        _callback?.Invoke(body.Slice(body.Length - reader.BytesRemaining));
    }

    private void NotifyItem(uint canId, ReadOnlyMemory<byte> body)
    {
        byte[] buffer = new byte[body.Length + 4];
        buffer[0] = 0xA1; // map with 1 element
        buffer[1] = 0x19; // ushort
        Span<byte> span = buffer;
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(2), CanID.GetDataID(canId));
        ReadOnlyMemory<byte> source = body;
        Memory<byte> memory = buffer;
        source.CopyTo(memory.Slice(4));
        _callback?.Invoke(buffer);
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
    }
}