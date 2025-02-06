/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Threading.Tasks;

namespace ThingSet.Common.Transports;

/// <summary>
/// Tentative interface for the transport layer of a ThingSet client.
/// </summary>
public interface IClientTransport : ITransport
{
    ValueTask ConnectAsync();
    /// <summary>
    /// Subscribes for asynchronous reports delivered via this
    /// transport.
    /// </summary>
    /// <param name="callback">A callback that is invoked when a
    /// report is received.</param>
    ValueTask SubscribeAsync(Action<ReadOnlyMemory<byte>> callback);

    /// <summary>
    /// Write data from a buffer to the transport.
    /// </summary>
    /// <param name="buffer">The buffer whose data should be written.</param>
    /// <param name="length">The number of bytes in the buffer to write.</param>
    /// <returns>True if data was written, otherwise false.</returns>
    bool Write(byte[] buffer, int length);
    /// <summary>
    /// Read data from the transport into a buffer.
    /// </summary>
    /// <param name="buffer">The buffer into which data should be read.</param>
    /// <returns>The number of bytes read.</returns>
    int Read(byte[] buffer);
}