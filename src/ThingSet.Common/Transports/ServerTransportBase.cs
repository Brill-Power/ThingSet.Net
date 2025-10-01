/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.IO;
using System.Threading.Tasks;

namespace ThingSet.Common.Transports;

/// <summary>
/// Base class for server transports.
/// </summary>
public abstract class ServerTransportBase : IServerTransport
{
    public abstract event EventHandler<ErrorEventArgs>? Error;

    public abstract ValueTask ListenAsync(Func<object, Memory<byte>, Memory<byte>> callback);

    public abstract void PublishReport(byte[] buffer);

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
