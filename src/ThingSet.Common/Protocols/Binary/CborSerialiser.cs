/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections;
using System.Formats.Cbor;

namespace ThingSet.Common.Protocols.Binary;

public static class CborSerialiser
{
    private static void WriteMap(CborWriter writer, IDictionary map)
    {
        writer.WriteStartMap(map.Count);
        foreach (object key in map.Keys)
        {
            Write(writer, key);
            Write(writer, map[key]);
        }
        writer.WriteEndMap();
    }

    private static void WriteArray(CborWriter writer, Array array)
    {
        writer.WriteStartArray(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            Write(writer, array.GetValue(i));
        }
        writer.WriteEndArray();
    }

    public static void Write(CborWriter writer, object? value)
    {
        switch (value)
        {
            case float f:
                writer.WriteSingle(f);
                break;
            case double d:
                writer.WriteDouble(d);
                break;
            case sbyte s when s < 0:
                writer.WriteInt32(s);
                break;
            case sbyte s when s >= 0:
                writer.WriteUInt32((byte)s);
                break;
            case byte b:
                writer.WriteUInt32(b);
                break;
            case short s when s < 0:
                writer.WriteInt32(s);
                break;
            case short s when s >= 0:
                writer.WriteUInt32((ushort)s);
                break;
            case ushort u:
                writer.WriteUInt32(u);
                break;
            case int i when i < 0:
                writer.WriteInt32(i);
                break;
            case int i when i >= 0:
                writer.WriteUInt32((uint)i);
                break;
            case uint ui:
                writer.WriteUInt32(ui);
                break;
            case long l when l < 0:
                writer.WriteInt64(l);
                break;
            case long l when l >= 0:
                writer.WriteUInt64((ulong)l);
                break;
            case ulong t:
                writer.WriteUInt64(t);
                break;
            case string s:
                writer.WriteTextString(s);
                break;
            case bool b:
                writer.WriteBoolean(b);
                break;
            case byte[] b:
                writer.WriteByteString(b);
                break;
            case IDictionary d:
                WriteMap(writer, d);
                break;
            case Array a:
                WriteArray(writer, a);
                break;
            default:
                if (value is null)
                {
                    writer.WriteNull();
                }
                else
                {
                    throw new NotSupportedException($"Data of type {value.GetType().Name} is not currently supported.");
                }
                break;
        }
    }
}