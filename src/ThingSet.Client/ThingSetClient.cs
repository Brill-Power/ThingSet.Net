/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
﻿using System;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ThingSet.Client.Schema;
using ThingSet.Common.Protocols;
using ThingSet.Common.Protocols.Binary;
using ThingSet.Common.Transports;

namespace ThingSet.Client;

/// <summary>
/// Client for ThingSet devices.
/// </summary>
public class ThingSetClient : IThingSetClient
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    private readonly IClientTransport _transport;

    private readonly Lazy<ThingSetSchema> _schema;

    private readonly object _lock = new object();

    private Action<uint, string?, object?>? _callback;

    public ThingSetClient(IClientTransport transport) : this(transport, new DefaultThingSetSchemaProvider())
    {
    }

    public ThingSetClient(IClientTransport transport, IThingSetSchemaProvider schemaProvider)
    {
        _transport = transport;
        _schema = new Lazy<ThingSetSchema>(() => schemaProvider.GetSchema(this));
    }

    /// <summary>
    /// Connects to the ThingSet device.
    /// </summary>
    public async ValueTask ConnectAsync()
    {
        await _transport.ConnectAsync();
    }

    /// <summary>
    /// Subscribes to ThingSet reports. The supplied callback will be invoked when
    /// a report is received.
    /// </summary>
    /// <param name="callback">A callback that will be invoked when a report
    /// is received.</param>
    public async ValueTask SubscribeAsync(Action<uint, string?, object?> callback)
    {
        ThingSetSchema schema = _schema.Value; // force evaluation
        _callback = callback;
        await _transport.SubscribeAsync(OnReportReceived);
    }

    public IEnumerable<ThingSetNode> GetNodes(ThingSetNodeEnumerationOptions options)
    {
        return _schema.Value.GetNodes(options);
    }

    public object? Get(uint id)
    {
        return DoRequest(ThingSetRequest.Get, cw => cw.WriteUInt32(id));
    }

    public object? Get(string path)
    {
        return DoRequest(ThingSetRequest.Get, cw => cw.WriteTextString(path));
    }

    public object? Fetch(uint id, object arg)
    {
        return DoRequest(ThingSetRequest.Fetch, cw =>
        {
            cw.WriteUInt32(id);
            CborSerialiser.Write(cw, arg);
        });
    }

    public object? Fetch(uint id, params object[] args)
    {
        return DoRequest(ThingSetRequest.Fetch, cw =>
        {
            cw.WriteUInt32(id);
            if (args.Length == 0)
            {
                cw.WriteNull();
            }
            else
            {
                CborSerialiser.Write(cw, args);
            }
        });
    }

    public object? Fetch(string path)
    {
        return DoRequest(ThingSetRequest.Fetch, cw =>
        {
            cw.WriteTextString(path);
            cw.WriteNull();
        });
    }

    public object? Update(string fullyQualifiedName, object value)
    {
        return DoRequest(ThingSetRequest.Update, cw =>
        {
            int index = fullyQualifiedName.LastIndexOf('/');
            string pathToParent = index > 0 ? fullyQualifiedName.Substring(0, index) : String.Empty;
            string key = fullyQualifiedName.Substring(index + 1);
            cw.WriteTextString(pathToParent);
            Dictionary<string, object> map = new Dictionary<string, object>
            {
                { key, value }
            };
            CborSerialiser.Write(cw, map);
        });
    }

    public object? Exec(uint id, params object[] args)
    {
        return DoRequest(ThingSetRequest.Exec, cw =>
        {
            cw.WriteUInt32(id);
            CborSerialiser.Write(cw, args);
        });
    }

    public object? Exec(string path, params object[] args)
    {
        return DoRequest(ThingSetRequest.Exec, cw =>
        {
            cw.WriteTextString(path);
            CborSerialiser.Write(cw, args);
        });
    }

    private object? DoRequest(ThingSetRequest action, Action<CborWriter> write)
    {
        byte[] buffer = new byte[4095];
        Span<byte> span = buffer;
        buffer[0] = (byte)action;
        CborWriter writer = new CborWriter(CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
        write(writer);
        writer.Encode(span.Slice(1));
        if (!Monitor.TryEnter(_lock, LockTimeout))
        {
            throw new TimeoutException("Timed out trying to send request.");
        }
        try
        {
            if (!_transport.Write(buffer, 1 + writer.BytesWritten))
            {
                throw new IOException("Could not connect to ThingSet endpoint.");
            }
            _transport.Read(buffer);
            ThingSetResponse response = (ThingSetStatus)buffer[0];
            if (response.Success)
            {
                CborReader reader = new CborReader(buffer.AsMemory().Slice(1), CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
                reader.ReadNull();
                return CborDeserialiser.Read(reader);
            }
            else
            {
                throw new ThingSetException($"ThingSet Error {response.Status}", response.Status);
            }
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    private void OnReportReceived(ReadOnlyMemory<byte> buffer)
    {
        CborReader reader = new CborReader(buffer);
        try
        {
            if (CborDeserialiser.Read(reader) is Dictionary<object, object> map)
            {
                foreach (uint key in map.Keys)
                {
                    _schema.Value.TryGetNode(key, out ThingSetNode? property);
                    object value = map[key];
                    _callback?.Invoke(key, property?.FullyQualifiedName, value);
                }
            }
        }
        catch (InvalidDataException)
        {
        }
        catch (CborContentException)
        {
        }
    }

    public void Dispose()
    {
        _transport.Dispose();
    }
}