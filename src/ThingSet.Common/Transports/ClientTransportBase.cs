/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Cbor;
using System.Threading;
using System.Threading.Tasks;
using ThingSet.Common.Protocols.Binary;

namespace ThingSet.Common.Transports;

/// <summary>
/// Base class for ThingSet client transports.
/// </summary>
/// <typeparam name="TEndpoint">Type of an identifier of a node on a network.</typeparam>
public abstract class ClientTransportBase<TEndpoint> : IClientTransport
    where TEndpoint : notnull
{
    protected readonly ConcurrentDictionary<TEndpoint, ReceiveBuffer> _buffersBySender = new ConcurrentDictionary<TEndpoint, ReceiveBuffer>();

    private Action<ulong?, CborReader>? _callback;

    private readonly Thread _subscriptionThread;
    private bool _runSubscriptionThread = true;

    protected ClientTransportBase()
    {
        _subscriptionThread = new Thread(RunSubscriptionThread)
        {
            IsBackground = true,
            Name = $"Subscription {Address}",
        };
    }

    /// <summary>
    /// String representation of a network identifier.
    /// </summary>
    protected abstract string Address { get; }

    /// <summary>
    /// Connects this transport.
    /// </summary>
    public abstract ValueTask ConnectAsync();

    public abstract int Read(byte[] buffer);

    public async ValueTask SubscribeAsync(Action<ulong?, CborReader> callback)
    {
        if (_callback is not null)
        {
            throw new InvalidOperationException("There is already a subscription established.");
        }

        _callback = callback;

        await SubscribeAsync();

        _subscriptionThread.Start();
    }

    protected abstract ValueTask SubscribeAsync();

    public abstract bool Write(byte[] buffer, int length);

    public void Dispose()
    {
        _runSubscriptionThread = false;
        if (_subscriptionThread.IsAlive)
        {
            _subscriptionThread.Join(1000);
        }
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    protected async void RunSubscriptionThread()
    {
        while (_runSubscriptionThread)
        {
            await HandleIncomingPublicationsAsync();
        }
    }

    protected abstract ValueTask HandleIncomingPublicationsAsync();

    protected void NotifyReport(ulong? eui, CborReader reader)
    {
        reader.ReadUInt32(); // subset ID
        _callback?.Invoke(eui, reader);
    }

    /// <summary>
    /// Common multi-frame message parsing logic.
    /// </summary>
    /// <typeparam name="TMessageType">Type of enum which indicates which part of a
    /// message a particular frame is.</typeparam>
    protected abstract class ReportParser<TMessageType>
        where TMessageType : Enum
    {
        /// <returns>True if a complete message has been assembled.</returns>
        public bool TryParse(byte sequenceNumber, byte messageNumber, TMessageType messageType,
            ReceiveBuffer buffer, byte[] data, [MaybeNullWhen(true)] out ulong? eui,
            [NotNullWhen(true)] out CborReader? reader)
        {
            eui = null;
            reader = null;

            if (IsFirst(messageType))
            {
                buffer.Started = true;
                buffer.MessageNumber = messageNumber;
            }
            else if (buffer.MessageNumber != messageNumber)
            {
                buffer.Reset();
                return false;
            }
            else if (!buffer.Started)
            {
                buffer.Reset();
                return false;
            }

            if (sequenceNumber == (buffer.Sequence++ & 0xF))
            {
                Memory<byte> memory = data;
                buffer.Append(memory.Slice(Offset));
            }
            if (IsLast(messageType))
            {
                ReadOnlyMemory<byte> memory = buffer.Buffer;
                reader = new CborReader(memory.Slice(1), CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
                if (buffer.Buffer[0] == (byte)ThingSetRequest.ReportEnhanced)
                {
                    eui = reader.ReadUInt64();
                }
                buffer.Reset();
                return true;
            }

            return false;
        }

        protected virtual int Offset => 0;

        protected abstract bool IsFirst(TMessageType type);
        protected abstract bool IsLast(TMessageType type);
    }

    protected class ReceiveBuffer
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

        public void Append(Memory<byte> source)
        {
            Memory<byte> destination = Buffer;
            source.CopyTo(destination.Slice(Position));
            Position += source.Length;
        }
    }
}
