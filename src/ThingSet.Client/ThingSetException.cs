/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using ThingSet.Common.Protocols;

namespace ThingSet.Client;

public class ThingSetException : Exception
{
    public ThingSetException(string message, ThingSetStatus errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public ThingSetStatus ErrorCode { get; }
}