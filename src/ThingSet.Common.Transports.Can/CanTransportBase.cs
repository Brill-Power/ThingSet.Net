/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using SocketCANSharp;
using SocketCANSharp.Network;

namespace ThingSet.Common.Transports.Can;

public abstract class CanTransportBase : ITransport
{
    protected readonly ThingSetCanInterface _canInterface;
    private bool _disposeInterface;

    protected CanTransportBase(ThingSetCanInterface canInterface, bool leaveOpen)
    {
        _canInterface = canInterface;
        _disposeInterface = !leaveOpen;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposeInterface)
        {
            _canInterface.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }

    protected static IsoTpCanSocket CreateIsoTpCanSocket(bool fd)
    {
        return new IsoTpCanSocket
        {
            BaseOptions = new CanIsoTpOptions
            {
                Flags = IsoTpFlags.CAN_ISOTP_WAIT_TX_DONE
            },
            LinkLayerOptions = new CanIsoTpLinkLayerOptions
            {
                TxFlags = fd ? CanFdFlags.CANFD_FDF : CanFdFlags.None,
                TxDataLength = fd ? SocketCanConstants.CANFD_MAX_DLEN : SocketCanConstants.CAN_MAX_DLEN,
                Mtu = fd ? SocketCanConstants.CANFD_MTU : SocketCanConstants.CAN_MTU,
            },
        };
    }
}