/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SocketCANSharp;
using SocketCANSharp.Network;

namespace ThingSet.Common.Transports.Can;

public class CanServerTransport : CanTransportBase, IServerTransport
{
    private readonly ConcurrentDictionary<byte, IsoTpCanSocket> _peerSocketsById = new();
    private readonly ConcurrentDictionary<byte, Thread> _peerSocketThreadsById = new();

    private readonly AddressClaimListener _addressClaimListener;
    private readonly RawCanSocket _publishSocket;

    /// <summary>
    /// Used in sequencing multi-frame reports.
    /// </summary>
    private byte _messageNumber;

    private bool _runPeerSocketHandlers = true;

    private Func<object, Memory<byte>, Memory<byte>>? _messageCallback;

    public CanServerTransport(ThingSetCanInterface canInterface) : base(canInterface, leaveOpen: false)
    {
        _publishSocket = new RawCanSocket
        {
            EnableCanFdFrames = canInterface.IsFdMode,
            SendBufferSize = 1024 * 1024,
        };
        _addressClaimListener = new AddressClaimListener(canInterface);
        _addressClaimListener.AddressClaimed += OnAddressClaimed;
    }

    public event EventHandler<ErrorEventArgs>? Error;

    public ValueTask ListenAsync(Func<object, Memory<byte>, Memory<byte>> callback)
    {
        _addressClaimListener.Listen();

        _messageCallback = callback;

        _publishSocket.Bind(_canInterface.Interface);

        return ValueTask.CompletedTask;
    }

    public void PublishControl(ushort id, byte[] buffer)
    {
        if (buffer.Length > _canInterface.FrameSize)
        {
            throw new ArgumentOutOfRangeException($"Control messages must fit in a single frame ({_canInterface.FrameSize} bytes).");
        }
        uint canId = CanID.CreateCanID(MessageType.SingleFrameReport, MessagePriority.ReportLow, _canInterface.NodeAddress, 0) | CanID.SetDataID(id);
        byte[] full = new byte[_canInterface.FrameSize];
        Buffer.BlockCopy(buffer, 0, full, 0, buffer.Length);
        if (_canInterface.IsFdMode)
        {
            WriteFdFrame(canId, full);
        }
        else
        {
            WriteFrame(canId, full);
        }
    }

    public void PublishReport(byte[] buffer)
    {
        int pos = 0;
        byte sequenceNumber = 0;
        while (pos < buffer.Length)
        {
            MultiFrameMessageType multiFrameMessageType;
            int frameSize = Math.Min(buffer.Length - pos, _canInterface.FrameSize);
            if (frameSize == _canInterface.FrameSize)
            {
                multiFrameMessageType = pos == 0 ? MultiFrameMessageType.First : MultiFrameMessageType.Consecutive;
            }
            else
            {
                multiFrameMessageType = pos == 0 ? MultiFrameMessageType.Single : MultiFrameMessageType.Last;
            }
            uint canId = CanID.CreateCanID(MessageType.MultiFrameReport, MessagePriority.ReportLow, _canInterface.NodeAddress, 0) | CanID.SetMessageNumber(_messageNumber) | (uint)multiFrameMessageType | CanID.SetSequenceNumber(sequenceNumber);
            byte[] subset = new byte[_canInterface.FrameSize];
            Buffer.BlockCopy(buffer, pos, subset, 0, frameSize);
            if (_canInterface.IsFdMode)
            {
                WriteFdFrame(canId, subset);
            }
            else
            {
                WriteFrame(canId, subset);
            }
            pos += frameSize;
            sequenceNumber++;
        }
        unchecked
        {
            _messageNumber++;
        }
    }

    private int WriteFrame(uint canId, byte[] buffer)
    {
        CanFrame frame = new CanFrame
        {
            CanId = canId,
            Data = buffer,
            Length = (byte)buffer.Length,
            Len8Dlc = (byte)buffer.Length,
        };
        return _publishSocket.Write(frame);
    }

    private int WriteFdFrame(uint canId, byte[] buffer)
    {

        CanFdFrame frame = new CanFdFrame
        {
            CanId = canId,
            Data = buffer,
            Length = (byte)buffer.Length,
        };
        return _publishSocket.Write(frame);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _runPeerSocketHandlers = false;
        foreach (Thread thread in _peerSocketThreadsById.Values)
        {
            thread.Join(1000);
        }
        foreach (IsoTpCanSocket socket in _peerSocketsById.Values)
        {
            socket.Dispose();
        }
    }

    private void RunPeerSocketHandler(object? state)
    {
        if (state is byte peerId)
        {
            IsoTpCanSocket socket = _peerSocketsById[peerId];
            while (_runPeerSocketHandlers)
            {
                byte[] buffer = new byte[4095];
                int read = socket.Read(buffer);
                if (read > 0 && _messageCallback is not null)
                {
                    try
                    {
                        Memory<byte> memory = buffer;
                        Memory<byte> response = _messageCallback(peerId, memory.Slice(0, read));
                        socket.Write(response.ToArray());
                    }
                    catch (Exception ex)
                    {
                        Error?.Invoke(this, new ErrorEventArgs(ex));
                    }
                }
            }
        }
    }

    private void OnAddressClaimed(object? sender, AddressEventArgs e)
    {
        if (!_peerSocketsById.ContainsKey(e.NodeAddress))
        {
            IsoTpCanSocket socket = CreateAndBindIsoTpCanSocket(e.NodeAddress);
            if (_peerSocketsById.TryAdd(e.NodeAddress, socket) &&
                _peerSocketThreadsById.TryAdd(e.NodeAddress, CreateAndStartPeerSocketThread(e.NodeAddress, socket)))
            {
                Console.WriteLine($"Found new peer {e.NodeAddress:x}");
            }
        }
    }

    private Thread CreateAndStartPeerSocketThread(byte peerId, IsoTpCanSocket socket)
    {
        Thread thread = new Thread(RunPeerSocketHandler)
        {
            IsBackground = true,
            Name = $"Peer {peerId:x}",
        };
        thread.Start(peerId);
        return thread;
    }

    private IsoTpCanSocket CreateAndBindIsoTpCanSocket(byte targetId)
    {
        IsoTpCanSocket socket = CreateIsoTpCanSocket(_canInterface.IsFdMode);
        socket.Bind(_canInterface.Interface,
            txId: CanID.CreateCanID(MessageType.RequestResponse, MessagePriority.Channel, _canInterface.NodeAddress, targetId),
            rxId: CanID.CreateCanID(MessageType.RequestResponse, MessagePriority.Channel, targetId, _canInterface.NodeAddress));
        return socket;
    }
}