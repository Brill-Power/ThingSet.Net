/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Transports.Can;

internal enum MultiFrameMessageType : uint
{
    First = 0x0 << CanID.MultiFrameTypePosition,
    Consecutive = 0x1 << CanID.MultiFrameTypePosition,
    Last = 0x2 << CanID.MultiFrameTypePosition,
    Single = 0x3 << CanID.MultiFrameTypePosition,
}