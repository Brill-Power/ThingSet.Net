/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Transports.Can;
    
internal enum MessagePriority : uint
{
    ControlEmergency = 0x0 << CanID.PriorityPosition,
    ControlHigh = 0x2 << CanID.PriorityPosition,
    ControlLow = 0x3 << CanID.PriorityPosition,
    NetworkManagement = 0x4 << CanID.PriorityPosition,
    ReportHigh = 0x5 << CanID.PriorityPosition,
    Channel = 0x6 << CanID.PriorityPosition,
    ReportLow = 0x7 << CanID.PriorityPosition,
}