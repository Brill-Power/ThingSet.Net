/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Cbor;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ThingSet.Common.Nodes;
using ThingSet.Common.Protocols;
using ThingSet.Common.Protocols.Binary;
using ThingSet.Common.Transports;

namespace ThingSet.Server;

public class ThingSetServer
{
    private readonly IServerTransport _transport;

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
            throw new NotSupportedException("Text mode is not currently supported.");
        }
        else
        {
            context = new ThingSetBinaryRequestContext(request);
        }

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
        return responseMem.Slice(0, length);
        // ThingSetResponse response = context.Handle();
        // byte[] buffer = new byte[response.Buffer.Length + 1];
        // buffer[0] = (byte)response.Status;
        // Buffer.BlockCopy(response.Buffer, 0, buffer, 1, response.Buffer.Length);
        // Console.WriteLine($"Sent response of {buffer.Length} bytes");
        // return buffer;
    }

    private int HandleExec(ThingSetRequestContextBase context, Memory<byte> response)
    {
        throw new NotImplementedException();
    }

    private int HandleUpdate(ThingSetRequestContextBase context, Memory<byte> response)
    {
        throw new NotImplementedException();
    }

    private int HandleFetch(ThingSetRequestContextBase context, Memory<byte> response)
    {
        if (context.HasValidEndpoint)
        {
            Span<byte> responseSpan = response.Span;
            responseSpan[0] = (byte)ThingSetStatus.Content;
            CborWriter writer = new CborWriter(CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
            writer.WriteNull();
            CborReader reader = new CborReader(context.RequestBody, CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
            if (reader.PeekState() == CborReaderState.Null)
            {
                if (context.Endpoint is ThingSetParentNode parent)
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
            else
            {
                responseSpan[0] = (byte)ThingSetStatus.NotImplemented;
                return 1;
            }
        }
        else
        {
            response.Span[0] = (byte)ThingSetStatus.NotFound;
            return 1;
        }
    }

    private int HandleGet(ThingSetRequestContextBase context, Memory<byte> response)
    {
        throw new NotImplementedException();
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
        else if (!String.IsNullOrEmpty(Path))
        {
            //
        }
        RequestBody = request.Slice(request.Length - _cborReader.BytesRemaining);
    }

    public override bool IsGet => _requestType == ThingSetBinaryRequestType.Get;
    public override bool IsFetch => _requestType == ThingSetBinaryRequestType.Fetch;
    public override bool IsUpdate => _requestType == ThingSetBinaryRequestType.Update;
    public override bool IsExec => _requestType == ThingSetBinaryRequestType.Exec;
}
