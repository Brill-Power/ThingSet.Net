/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Protocols.Binary;

public enum ThingSetRequest : byte
{
    Get    = 0x01, /**< Function code for GET request in binary mode. */
    Exec   = 0x02, /**< Function code for EXEC request in binary mode. */
    Delete = 0x04, /**< Function code for DELETE request in binary mode. */
    Fetch  = 0x05, /**< Function code for FETCH request in binary mode. */
    Create = 0x06, /**< Function code for CREATE request in binary mode. */
    Update = 0x07, /**< Function code for UPDATE request in binary mode. */
    Desire = 0x1D, /**< Function code for DESIRE in binary mode. */
    Report = 0x1F, /**< Function code for REPORT in binary mode. */
}