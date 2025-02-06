/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Cbor;
using System.Linq.Expressions;
using System.Reflection;

namespace ThingSet.Common.Protocols.Binary;

public class ReportDecoder
{
    protected static readonly MethodInfo DecodeArrayMethod = typeof(ReportDecoder).GetMethod(nameof(ReportDecoder.DecodeArray), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    protected static readonly MethodInfo DecodeMethod = typeof(ReportDecoder).GetMethod(nameof(ReportDecoder.Decode), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

    protected static TValue[] DecodeArray<TValue>(CborReader reader, Func<CborReader, TValue> valueReader)
    {
        int? count = reader.ReadStartArray();
        if (count.HasValue)
        {
            TValue[] array = new TValue[count.Value];
            for (int i = 0; i < count.Value; i++)
            {
                array[i] = valueReader(reader);
            }
            reader.ReadEndArray();
            return array;
        }
        else
        {
            List<TValue> array = new List<TValue>();
            CborReaderState state = reader.PeekState();
            while (state != CborReaderState.EndArray)
            {
                array.Add(valueReader(reader));
            }
            reader.ReadEndArray();
            return array.ToArray();
        }
    }

    protected static TValue? Decode<TValue>(CborReader reader)
        where TValue : class, new()
    {
        ReportDecoder<TValue> decoder = new ReportDecoder<TValue>();
        if (decoder.TryDecode(reader, out TValue? item))
        {
            return item;
        }
        return null;
    }

    protected static Expression GetReadExpression(Type valueType, Type declaredType, ParameterExpression readerPex)
    {
        Expression getValueEx;
        switch (Type.GetTypeCode(valueType))
        {
            case TypeCode.Single:
            case TypeCode.Double: // assume doubles are singles on the wire for now
                getValueEx = Expression.Call(readerPex, typeof(CborReader).GetMethod(nameof(CborReader.ReadSingle))!);
                break;
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                getValueEx = Expression.Call(readerPex, typeof(CborReader).GetMethod(nameof(CborReader.ReadUInt64))!);
                break;
            case TypeCode.String:
                getValueEx = Expression.Call(readerPex, typeof(CborReader).GetMethod(nameof(CborReader.ReadTextString))!);
                break;
            case TypeCode.Boolean:
                getValueEx = Expression.Call(readerPex, typeof(CborReader).GetMethod(nameof(CborReader.ReadBoolean))!);
                break;
            case TypeCode.Object:
                if (valueType.IsArray)
                {
                    Type elementType = valueType.GetElementType()!;
                    Type elementValueType = Nullable.GetUnderlyingType(elementType) ?? elementType;
                    MethodInfo decodeArray = DecodeArrayMethod.MakeGenericMethod(elementValueType);
                    ParameterExpression nestedReaderPex = Expression.Parameter(typeof(CborReader), "reader");
                    Expression readValueEx = GetReadExpression(elementValueType, elementType, nestedReaderPex);
                    LambdaExpression reader = Expression.Lambda(readValueEx, nestedReaderPex);
                    getValueEx = Expression.Call(decodeArray, readerPex, reader);
                }
                else
                {
                    MethodInfo decode = DecodeMethod.MakeGenericMethod(valueType);
                    getValueEx = Expression.Call(decode, readerPex);
                }
                break;
            default:
                throw new NotSupportedException($"Type {declaredType} is not currently supported.");
        }

        if (getValueEx.Type != declaredType)
        {
            getValueEx = Expression.Convert(getValueEx, declaredType);
        }

        return getValueEx;
    }
}

public class ReportDecoder<T> : ReportDecoder where T : class, new()
{
    private static readonly Dictionary<uint, Action<T, CborReader>> FieldDecodersByID = new Dictionary<uint, Action<T, CborReader>>();

    static ReportDecoder()
    {
        ParameterExpression itemPex = Expression.Parameter(typeof(T), "item");
        ParameterExpression readerPex = Expression.Parameter(typeof(CborReader), "reader");
        foreach (PropertyInfo property in typeof(T).GetProperties())
        {
            if (!property.CanWrite)
            {
                continue;
            }

            ThingSetReportFieldAttribute? rfa = property.GetCustomAttribute<ThingSetReportFieldAttribute>();
            if (rfa is null)
            {
                continue;
            }

            Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            bool isNullable = propertyType != property.PropertyType;

            MemberExpression propertyEx = Expression.Property(itemPex, property);
            Expression getValueEx = GetReadExpression(propertyType, property.PropertyType, readerPex);

            Action<T, CborReader> setter = Expression.Lambda<Action<T, CborReader>>(
                Expression.Assign(
                    propertyEx,
                    getValueEx
                ),
                itemPex,
                readerPex
            )
            .Compile();
            FieldDecodersByID.Add(rfa.ID, setter);
        }
    }

    public bool TryDecode(CborReader reader, [NotNullWhen(true)] out T? item)
    {
        item = default;
        CborReaderState state = reader.PeekState();
        if (state != CborReaderState.StartMap)
        {
            return false;
        }

        item = new T();
        int? count = reader.ReadStartMap();
        do
        {
            uint key = reader.ReadUInt32();
            if (FieldDecodersByID.TryGetValue(key, out Action<T, CborReader>? decoder))
            {
                decoder(item, reader);
            }
            else
            {
                reader.SkipValue();
            }
            state = reader.PeekState();
        } while (state != CborReaderState.EndMap);
        reader.ReadEndMap();

        return true;
    }

    public bool TryDecode(Memory<byte> buffer, [NotNullWhen(true)] out T? item)
    {
        CborReader reader = new CborReader(buffer, CborConformanceMode.Lax);
        return TryDecode(reader, out item);
    }
}