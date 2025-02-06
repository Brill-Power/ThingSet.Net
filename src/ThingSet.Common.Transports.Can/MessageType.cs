/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Transports.Can;

internal enum MessageType : uint
{
    RequestResponse = 0x0 << CanID.TypePosition,
    MultiFrameReport = 0x1 << CanID.TypePosition,
    SingleFrameReport = 0x2 << CanID.TypePosition,
    Network = 0x3 << CanID.TypePosition,
}