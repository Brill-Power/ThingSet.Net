/*
 * Copyright (c) 2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Transports.Ip;

public class Protocol
{
    public const int RequestResponsePort = 9001;
    public const int PublishSubscribePort = 9002;

    private const int MessageTypePosition = 4;

    internal enum MessageType
    {
        First = 0x0 << MessageTypePosition,
        Consecutive = 0x1 << MessageTypePosition,
        Last = 0x2 << MessageTypePosition,
        Single = 0x3 << MessageTypePosition,
    }
}