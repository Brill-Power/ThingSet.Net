/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Cbor;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ThingSet.Common.Nodes;
using ThingSet.Common.Protocols;
using ThingSet.Common.Protocols.Binary;
using ThingSet.Common.Transports;

namespace ThingSet.Server;

public class ThingSetServer : IDisposable
{
    private readonly IServerTransport _transport;

    private  const ushort MetadataNameId = 0x1a;
    private const ushort MetadataTypeId = 0x1b;
    private const ushort MetadataAccessId = 0x1c;

    public ThingSetServer(IServerTransport transport)
    {
        _transport = transport;
    }

    public async ValueTask ListenAsync()
    {
        await _transport.ListenAsync(OnRequestReceived);
    }

    private Memory<byte> OnRequestReceived(object identifier, Memory<byte> request)
    {
        byte[] response = new byte[8192];
        Memory<byte> responseMem = response;
        ThingSetRequestContextBase context;
        Span<byte> span = request.Span;
        int length;
        if (span[0] > 0x20)
        {
            // don't support text mode at the moment
            return Encoding.UTF8.GetBytes($":{(byte)ThingSetStatus.NotImplemented:X2}");
        }
        else
        {
            context = new ThingSetBinaryRequestContext(request);
        }
        if (context.HasValidEndpoint)
        {
            if (context.IsGet)
            {
                length = HandleGet(context, responseMem);
            }
            else if (context.IsFetch)
            {
                length = HandleFetch(context, responseMem);
            }
            else if (context.IsUpdate)
            {
                length = HandleUpdate(context, responseMem);
            }
            else if (context.IsExec)
            {
                length = HandleExec(context, responseMem);
            }
            else
            {
                response[0] = (byte)ThingSetStatus.BadRequest;
                length = 1;
            }
        }
        else
        {
            response[0] = (byte)ThingSetStatus.NotFound;
            length = 1;
        }
        return responseMem.Slice(0, length);
    }

    private int HandleExec(ThingSetRequestContextBase context, Memory<byte> response)
    {
        Span<byte> responseSpan = response.Span;
        if ((context.Endpoint?.Type.IsFunction ?? false) &&
            context.Endpoint is IThingSetFunction function)
        {
            CborReader reader = new CborReader(context.RequestBody, CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
            if (CborDeserialiser.Read(reader) is object[] args)
            {
                ParameterInfo[] parameters = function.Function.Method.GetParameters();
                if (args.Length != parameters.Length)
                {
                    responseSpan[0] = (byte)ThingSetStatus.BadRequest;
                    return 1;
                }

                for (int i = 0; i < args.Length; i++)
                {
                    args[i] = Convert.ChangeType(args[i], parameters[i].ParameterType);
                }
                responseSpan[0] = (byte)ThingSetStatus.Changed;
                object? result = function.Function.DynamicInvoke(args);
                CborWriter writer = new CborWriter(CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
                writer.WriteNull();
                CborSerialiser.Write(writer, result);
                writer.Encode(responseSpan.Slice(1));
                return writer.BytesWritten + 1;
            }
            else
            {
                responseSpan[0] = (byte)ThingSetStatus.BadRequest;
                return 1;
            }
        }
        else
        {
            responseSpan[0] = (byte)ThingSetStatus.MethodNotAllowed;
            return 1;
        }
    }

    private int HandleUpdate(ThingSetRequestContextBase context, Memory<byte> response)
    {
        throw new NotImplementedException();
    }

    private int HandleFetch(ThingSetRequestContextBase context, Memory<byte> response)
    {
        Span<byte> responseSpan = response.Span;
        responseSpan[0] = (byte)ThingSetStatus.Content;
        CborWriter writer = new CborWriter(CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
        writer.WriteNull();
        CborReader reader = new CborReader(context.RequestBody, CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
        if (reader.PeekState() == CborReaderState.Null)
        {
            if (context.Endpoint is IThingSetParentNode parent)
            {
                if (context.UseIds)
                {
                    CborSerialiser.Write(writer, parent.Children.Select(c => c.Id).ToArray());
                }
                else
                {
                    CborSerialiser.Write(writer, parent.Children.Select(c => c.Name).ToArray());
                }
                writer.Encode(responseSpan.Slice(1));
                return writer.BytesWritten + 1;
            }
            else
            {
                responseSpan[0] = (byte)ThingSetStatus.BadRequest;
                return 1;
            }
        }
        else if (context.Endpoint == ThingSetRegistry.Metadata &&
            reader.PeekState() == CborReaderState.StartArray)
        {
            if (CborDeserialiser.Read(reader) is object?[] ids)
            {
                List<Dictionary<ushort, object>> metadata = new List<Dictionary<ushort, object>>();
                foreach (uint id in ids.OfType<uint>())
                {
                    if (ThingSetRegistry.TryGetNode((ushort)id, out ThingSetNode? node))
                    {
                        metadata.Add(new Dictionary<ushort, object>
                        {
                            { MetadataNameId, node.Name },
                            { MetadataTypeId, node.Type.Type },
                            { MetadataAccessId, ThingSetAccess.AnyReadWrite }, // TODO
                        });
                    }
                }
                CborSerialiser.Write(writer, metadata.ToArray());
                writer.Encode(responseSpan.Slice(1));
                return writer.BytesWritten + 1;
            }
            else
            {
                responseSpan[0] = (byte)ThingSetStatus.BadRequest;
                return 1;
            }
        }
        else
        {
            responseSpan[0] = (byte)ThingSetStatus.NotImplemented;
            return 1;
        }
    }

    private int HandleGet(ThingSetRequestContextBase context, Memory<byte> response)
    {
        Span<byte> responseSpan = response.Span;
        responseSpan[0] = (byte)ThingSetStatus.Content;
        CborWriter writer = new CborWriter(CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
        writer.WriteNull();
        if (context.Endpoint is IThingSetParentNode parent)
        {
            if (context.UseIds)
            {
                CborSerialiser.Write(writer, GetKeyValuePairs(n => n.Id, parent.Children));
            }
            else
            {
                CborSerialiser.Write(writer, GetKeyValuePairs(n => n.Name, parent.Children));
            }
            writer.Encode(responseSpan.Slice(1));
            return 1 + writer.BytesWritten;
        }
        else if (context.Endpoint is IThingSetValue value)
        {
            CborSerialiser.Write(writer, value.Value);
            writer.Encode(responseSpan.Slice(1));
            return 1 + writer.BytesWritten;
        }
        else
        {
            responseSpan[0] = (byte)ThingSetStatus.UnsupportedFormat;
            return 1;
        }
    }

    private Dictionary<TKey, object?> GetKeyValuePairs<TKey>(Func<ThingSetNode, TKey> keySelector, IEnumerable<ThingSetNode> nodes)
        where TKey : IEquatable<TKey>
    {
        Dictionary<TKey, object?> keyValuePairs = new Dictionary<TKey, object?>();
        foreach (ThingSetNode node in nodes)
        {
            if (node is IThingSetValue value)
            {
                keyValuePairs.Add(keySelector(node), value.Value);
            }
        }
        return keyValuePairs;
    }

    public void Dispose()
    {
        _transport.Dispose();
    }
}

internal abstract class ThingSetRequestContextBase
{
    protected readonly Memory<byte> _request;

    protected ThingSetRequestContextBase(Memory<byte> request)
    {
        _request = request;
    }

    public string? Path { get; protected set; }
    public ushort? Id { get; protected set; }
    public ThingSetNode? Endpoint { get; protected set; }

    public bool UseIds => Id.HasValue;
    [MemberNotNullWhen(true, nameof(Endpoint))]
    public bool HasValidEndpoint => Endpoint is not null;

    public Memory<byte> RequestBody { get; protected set; }

    public abstract bool IsGet { get; }
    public abstract bool IsFetch { get; }
    public abstract bool IsUpdate { get; }
    public abstract bool IsExec{ get; }
}

internal class ThingSetBinaryRequestContext : ThingSetRequestContextBase
{
    private readonly ThingSetBinaryRequestType _requestType;
    private readonly CborReader _cborReader;

    internal ThingSetBinaryRequestContext(Memory<byte> request) : base(request)
    {
        _requestType = (ThingSetBinaryRequestType)request.Span[0];
        _cborReader = new CborReader(request.Slice(1), CborConformanceMode.Lax, allowMultipleRootLevelValues: true);

        CborReaderState state = _cborReader.PeekState();
        switch (state)
        {
            case CborReaderState.TextString:
                Path = _cborReader.ReadTextString();
                break;
            case CborReaderState.UnsignedInteger:
                Id = (ushort)_cborReader.ReadInt32();
                break;
            case CborReaderState.NegativeInteger: // unlikey to be this, but let's be pragmatic
                Id = (ushort)_cborReader.ReadInt32();
                break;
            default:
                throw new InvalidDataException($"Unexpected CBOR data of type {state} (expected string or integer).");
        }

        if (Id.HasValue && ThingSetRegistry.TryGetNode(Id.Value, out ThingSetNode? node))
        {
            Endpoint = node;
        }
        else if (Path != null) // empty is valid
        {
            if (ThingSetRegistry.TryGetNode(Path, out node, out ThingSetStatus? error))
            {
                Endpoint = node;
            }
            else
            {
                // how to signal error?
            }
        }
        RequestBody = request.Slice(request.Length - _cborReader.BytesRemaining);
    }

    public override bool IsGet => _requestType == ThingSetBinaryRequestType.Get;
    public override bool IsFetch => _requestType == ThingSetBinaryRequestType.Fetch;
    public override bool IsUpdate => _requestType == ThingSetBinaryRequestType.Update;
    public override bool IsExec => _requestType == ThingSetBinaryRequestType.Exec;
}
