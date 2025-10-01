/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.IO;

namespace ThingSet.Common.Protocols.Binary;

public static class CborDeserialiser
{
    public static object? Read(CborReader reader)
    {
        CborReaderState state = reader.PeekState();
        switch (state)
        {
            case CborReaderState.NegativeInteger:
            case CborReaderState.UnsignedInteger:
                return ReadInteger(reader, state);
            case CborReaderState.HalfPrecisionFloat:
                return (float)reader.ReadHalf();
            case CborReaderState.SinglePrecisionFloat:
                return reader.ReadSingle();
            case CborReaderState.DoublePrecisionFloat:
                return reader.ReadDouble();
            case CborReaderState.TextString:
                return reader.ReadTextString();
            case CborReaderState.ByteString:
                return reader.ReadByteString();
            case CborReaderState.StartMap:
                return ReadMap(reader);
            case CborReaderState.StartArray:
                return ReadArray(reader);
            case CborReaderState.Boolean:
                return reader.ReadBoolean();
            case CborReaderState.Null:
                reader.ReadNull();
                return null;
            case CborReaderState.SimpleValue:
                switch (reader.ReadSimpleValue())
                {
                    case CborSimpleValue.True:
                        return true;
                    case CborSimpleValue.False:
                        return false;
                    case CborSimpleValue.Null:
                        return null;
                    case CborSimpleValue.Undefined:
                    default:
                        throw new InvalidDataException($"Received invalid data.");
                }

            default:
                throw new NotSupportedException($"Unsupported data format {reader.PeekState()}");
        }
    }

    private static object ReadInteger(CborReader reader, CborReaderState state)
    {
        switch (state)
        {
            case CborReaderState.NegativeInteger:
                long dragon = reader.ReadInt64();
                if (dragon < Int32.MinValue || dragon > Int32.MaxValue)
                {
                    return dragon;
                }
                return (int)dragon;
            case CborReaderState.UnsignedInteger:
                ulong t = reader.ReadUInt64();
                if (t < UInt32.MinValue || t > UInt32.MaxValue)
                {
                    return t;
                }
                return (uint)t;
            default:
                throw new InvalidDataException($"Unsupported integer type {state}.");
        }
    }

    private static Array ReadArray(CborReader reader)
    {
        int? count = reader.ReadStartArray();
        if (count.HasValue)
        {
            object?[] array = new object?[count.Value];
            for (int i = 0; i < count.Value; i++)
            {
                array[i] = Read(reader);
            }
            reader.ReadEndArray();
            return array;
        }
        else
        {
            List<object?> array = new List<object?>();
            CborReaderState state = reader.PeekState();
            while (state != CborReaderState.EndArray)
            {
                array.Add(Read(reader));
                state = reader.PeekState();
            }
            reader.ReadEndArray();
            return array.ToArray();
        }
    }

    private static Dictionary<object, object?> ReadMap(CborReader reader)
    {
        Dictionary<object, object?> map = new Dictionary<object, object?>();
        int? count = reader.ReadStartMap();
        CborReaderState state = reader.PeekState();
        while (state != CborReaderState.EndMap)
        {
            object? key = Read(reader);
            object? value = Read(reader);
            if (key is not null)
            {
                map[key] = value;
            }
            state = reader.PeekState();
        };
        reader.ReadEndMap();
        return map;
    }
}