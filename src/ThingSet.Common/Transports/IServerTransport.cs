/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.IO;
using System.Threading.Tasks;

namespace ThingSet.Common.Transports;

/// <summary>
/// Interface for transports for ThingSet servers.
/// </summary>
public interface IServerTransport : ITransport
{
    ValueTask ListenAsync(Func<object, Memory<byte>, Memory<byte>> callback);

    event EventHandler<ErrorEventArgs>? Error;

    void PublishReport(byte[] buffer);
}