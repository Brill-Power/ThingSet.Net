/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Buffers.Binary;
using System.Threading;
using SocketCANSharp;
using SocketCANSharp.Network;

namespace ThingSet.Common.Transports.Can;

/// <summary>
/// Listens for ThingSet address claim message broadcasts on a CAN bus.
/// </summary>
public class AddressClaimListener : IAddressClaimListener
{
    private readonly ThingSetCanInterface _canInterface;

    private readonly RawCanSocket _peerAddressesHandlerCanSocket;

    private readonly Thread _peerAddressesHandlerThread;
    private bool _runPeerAddressesHandler = true;

    public AddressClaimListener(ThingSetCanInterface canInterface)
    {
        _canInterface = canInterface;

        _peerAddressesHandlerCanSocket = new RawCanSocket
        {
            EnableCanFdFrames = canInterface.IsFdMode,
            CanFilters =
            [
                new CanFilter(canId: (uint)MessageType.Network | CanID.SetTarget(CanID.BroadcastAddress),
                    canMask: CanID.TypeMask | CanID.TargetMask),
            ]
        };

        _peerAddressesHandlerThread = new Thread(RunPeerAddressesHandler)
        {
            IsBackground = true,
        };
    }

    public event EventHandler<AddressClaimEventArgs>? AddressClaimed;

    public void Dispose()
    {
        _runPeerAddressesHandler = false;
        if (_peerAddressesHandlerThread.IsAlive)
        {
            _peerAddressesHandlerThread.Join(1000);
        }
        _peerAddressesHandlerCanSocket.Dispose();
    }

    public void Listen()
    {
        _peerAddressesHandlerCanSocket.Bind(_canInterface.Interface);
        _peerAddressesHandlerThread.Start();
    }

    private void RunPeerAddressesHandler()
    {
        while (_runPeerAddressesHandler)
        {
            if (_peerAddressesHandlerCanSocket.Read(out CanFrame readFrame) > 0)
            {
                byte peerId = CanID.GetSource(readFrame.CanId);
                if (peerId != _canInterface.NodeAddress)
                {
                    byte bridgeId = CanID.GetBridge(readFrame.CanId);
                    ReadOnlySpan<byte> data = readFrame.Data;
                    ulong nodeId = BinaryPrimitives.ReadUInt64BigEndian(readFrame.Data);
                    AddressClaimed?.Invoke(this, new AddressClaimEventArgs(peerId, bridgeId, nodeId));
                }
            }
        }
    }
}