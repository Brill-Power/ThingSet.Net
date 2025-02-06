/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;

namespace ThingSet.Common.Protocols;

[AttributeUsage(AttributeTargets.Property)]
public class ThingSetReportFieldAttribute : Attribute
{
    public ThingSetReportFieldAttribute()
    {
    }

    public ThingSetReportFieldAttribute(ushort id)
    {
        ID = id;
    }

    public ushort ID { get; set; }
}