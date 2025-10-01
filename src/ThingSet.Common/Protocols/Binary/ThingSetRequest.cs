/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Protocols.Binary;

public enum ThingSetRequest : byte
{
    Get     = 0x01,
    Exec    = 0x02,
    Delete  = 0x04,
    Fetch   = 0x05,
    Create  = 0x06,
    Update  = 0x07,
    Forward = 0x1C,
    Desire  = 0x1D,
    ReportEnhanced = 0x1E,
    Report = 0x1F,
}