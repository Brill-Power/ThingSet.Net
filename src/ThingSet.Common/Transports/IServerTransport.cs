/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Threading.Tasks;

namespace ThingSet.Common.Transports;

public interface IServerTransport : ITransport
{
    ValueTask ListenAsync(Func<byte, byte[], byte[]> callback);

    void PublishControl(ushort id, byte[] buffer);
    void PublishReport(byte[] buffer);
}