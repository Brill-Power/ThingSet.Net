/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace ThingSet.Common;

/// <summary>
/// Represents the type of an item in ThingSet.
/// </summary>
public struct ThingSetType : IEquatable<ThingSetType>
{
    public static readonly ThingSetType Boolean = "bool";
    public static readonly ThingSetType UInt8 = "u8";
    public static readonly ThingSetType Int8 = "i8";
    public static readonly ThingSetType UInt16 = "u16";
    public static readonly ThingSetType Int16 = "i16";
    public static readonly ThingSetType UInt32 = "u32";
    public static readonly ThingSetType Int32 = "i32";
    public static readonly ThingSetType UInt64 = "u64";
    public static readonly ThingSetType Int64 = "i64";
    public static readonly ThingSetType Float = "f32";
    public static readonly ThingSetType Double = "f64";
    public static readonly ThingSetType Decimal = "decimal";
    public static readonly ThingSetType String = "string";
    public static readonly ThingSetType Buffer = "buffer";
    public static readonly ThingSetType Array = "array";
    public static readonly ThingSetType Record = "record";
    public static readonly ThingSetType Group = "group";
    public static readonly ThingSetType Subset = "subset";
    public static readonly ThingSetType FunctionVoid = "()->()";
    public static readonly ThingSetType FunctionInt32 = "()->(i32)";

    private ThingSetType(string type)
    {
        Type = type;
    }

    public string Type { get; }

    /// <summary>
    /// Gets whether this value represents an array type. The type of
    /// the elements in the array can be retrieved via the
    /// <see cref="ElementType"/> property.
    /// </summary>
    public bool IsArray => Type.EndsWith(']');
    /// <summary>
    /// Gets whether this value represents a record type.
    /// </summary>
    public bool IsRecord => Type.StartsWith("record");
    /// <summary>
    /// Gets whether this value represents a function type.
    /// </summary>
    public bool IsFunction => Type[0] == '(' && Type.Contains("->");

    /// <summary>
    /// If this value represents an array type, gets the type of the elements in the array.
    /// </summary>
    public ThingSetType? ElementType => IsArray ? Type.Substring(0, Type.Length - 2) : (ThingSetType?)null;

    public static ThingSetType GetType(Type parameterType)
    {
        parameterType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;

        switch (System.Type.GetTypeCode(parameterType))
        {
            case TypeCode.Boolean:
                return Boolean;
            case TypeCode.Byte:
                return UInt8;
            case TypeCode.SByte:
                return Int8;
            case TypeCode.UInt16:
                return UInt16;
            case TypeCode.Int16:
                return Int16;
            case TypeCode.UInt32:
                return UInt32;
            case TypeCode.Int32:
                return Int32;
            case TypeCode.UInt64:
                return UInt64;
            case TypeCode.Int64:
                return Int64;
            case TypeCode.Single:
                return Float;
            case TypeCode.Double:
                return Double;
            case TypeCode.Decimal:
                return Decimal;
            case TypeCode.String:
                return String;
            default:
                if (parameterType == typeof(void))
                {
                    return new ThingSetType(System.String.Empty);
                }
                if (parameterType.IsArray)
                {
                    return $"{GetType(parameterType.GetElementType()!)}";
                }
                if (parameterType.IsEnum)
                {
                    return GetType(parameterType.GetEnumUnderlyingType());
                }
                if (typeof(Delegate).IsAssignableFrom(parameterType))
                {
                    if (parameterType.IsGenericType)
                    {
                        Type[] types = parameterType.GetGenericArguments();
                        Type delegateType = parameterType.GetGenericTypeDefinition();
                        IEnumerable<Type> args = types;
                        Type returnType;
                        if (delegateType.Name.StartsWith("Func"))
                        {
                            // last arg is return type
                            args = types.SkipLast(1);
                            returnType = types[types.Length - 1];
                        }
                        else
                        {
                            // returns void
                            returnType = typeof(void);
                        }
                        return $"({System.String.Join(",", args.Select(a => GetType(a)))})->({GetType(returnType)})";
                    }
                    return "()->()"; // don't think we can reflectively get the type
                }
                return "unknown";
        }
    }

    public bool Equals(ThingSetType other) => Type == other.Type;

    public override bool Equals(object? other) => other is ThingSetType type ? Equals(type) : false;

    public override int GetHashCode() => Type.GetHashCode();

    public override string ToString() => Type;

    public static bool operator ==(ThingSetType left, ThingSetType right) => left.Equals(right);

    public static bool operator !=(ThingSetType left, ThingSetType right) => !left.Equals(right);

    public static implicit operator string(ThingSetType type) => type.Type;

    public static implicit operator ThingSetType(string type) => new ThingSetType(type);
}