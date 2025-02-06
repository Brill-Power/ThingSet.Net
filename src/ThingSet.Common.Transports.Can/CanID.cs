/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using SocketCANSharp;

namespace ThingSet.Common.Transports.Can;

internal static class CanID
{
    /*
    * ThingSet addressing in 29-bit CAN ID
    *
    * Request/response messages using ISO-TP (bus forwarding scheme):
    *
    *    28      26 25 24 23     20 19     16 15            8 7             0
    *   +----------+-----+---------+---------+---------------+---------------+
    *   | Priority | 0x0 | tgt bus | src bus |  target addr  |  source addr  |
    *   +----------+-----+---------+---------+---------------+---------------+
    *
    *   Priority: 6
    *
    *   tgt bus: Bus number of the target node (default for single bus systems is 0x0)
    *   src bus: Bus number of the source node (default for single bus systems is 0x0)
    *
    * Request/response messages using ISO-TP (bridge forwarding scheme):
    *
    *    28      26 25 24 23      16 15            8 7             0
    *   +----------+-----+----------+---------------+---------------+
    *   | Priority | 0x0 |  bridge  |  target addr  |  source addr  |
    *   +----------+-----+----------+---------------+---------------+
    *
    *   Priority: 6
    *
    *   bridge: Bridge number for message forwarding (0x00 for local communication)
    *
    * Multi-frame reports:
    *
    *    28      26 25 24 23 20 19     16 15  13   12  11   8 7           0
    *   +----------+-----+-----+---------+------+-----+------+-------------+
    *   | Priority | 0x1 | res | src bus | msg# | end | seq# | source addr |
    *   +----------+-----+-----+---------+------+-----+------+-------------+
    *
    *   Priority: 5 or 7
    *   msg#: Wrapping message counter from 0 to 7
    *   end: End of message flag
    *   seq#: Wrapping sequence counter from 0 to 15
    *   src bus and res: Either source bus or bridge number
    *
    * Single-frame reports:
    *
    *    28      26 25 24 23           16 15            8 7             0
    *   +----------+-----+---------------+---------------+---------------+
    *   | Priority | 0x2 | data ID (MSB) | data ID (LSB) |  source addr  |
    *   +----------+-----+---------------+---------------+---------------+
    *
    *   Priority:
    *     0 .. 3: High-priority control frames
    *     5, 7: Normal report frames for monitoring
    *
    * Network management (e.g. address claiming):
    *
    *    28      26 25 24 23           16 15            8 7             0
    *   +----------+-----+---------------+---------------+---------------+
    *   | Priority | 0x3 | variable byte |  target addr  |  source addr  |
    *   +----------+-----+---------------+---------------+---------------+
    *
    *   Priority: 4
    *
    *   Variable byte:
    *     - Random data for address discovery frame
    *     - Bus ID for address claiming frame (same as request/response)
    */

    /* source and target addresses */
    public const int SourcePosition = 0;
    public const uint SourceMask = 0xFF << SourcePosition;
    public static uint SetSource(byte addr) => (((uint)addr << SourcePosition) & SourceMask);
    public static byte GetSource(uint id) => (byte)((id & SourceMask) >> SourcePosition);

    public const int TargetPosition = 8;
    public const uint TargetMask = 0xFF << TargetPosition;
    public static uint SetTarget(byte addr) => (((uint)addr << TargetPosition) & TargetMask);
    public static byte GetTarget(uint id) => (byte)((id & TargetMask) >> TargetPosition);

    public const byte MinAddress = 0x01;
    public const byte MaxAddress = 0xFD;
    public const byte AnonymousAddress = 0xFE;
    public const byte BroadcastAddress = 0xFF;

    /* data IDs for single-frame reports */
    public const int DataIDPosition = 8;
    public const uint DataIDMask = 0xFFFF << DataIDPosition;
    public static uint SetDataID(ushort id) => (((uint)id << DataIDPosition) & DataIDMask);
    public static ushort GetDataID(uint id) => (ushort)((id & DataIDMask) >> DataIDPosition);

    /* message number, type and sequence number for multi-frame reports */
    public const int SequenceNumberPosition = 8;
    public const uint SequenceNumberMask = 0xF << SequenceNumberPosition;
    public static uint SetSequenceNumber(byte no) => (((uint)no << SequenceNumberPosition) & SequenceNumberMask);
    public static byte GetSequenceNumber(uint id) => (byte)(((uint)id & SequenceNumberMask) >> SequenceNumberPosition);

    public const int MultiFrameTypePosition = 12;
    public const uint MultiFrameTypeMask = 0x3 << MultiFrameTypePosition;
    public static MultiFrameMessageType GetMultiFrameMessageType(uint id) => (MultiFrameMessageType)(id & MultiFrameTypeMask);

    public const int MessageNumberPosition = 14;
    public const uint MessageNumberMask = 0x3 << MessageNumberPosition;
    public static uint SetMessageNumber(byte no) => (((uint)no << MessageNumberPosition) & MessageNumberMask);
    public static byte GetMessageNumber(uint id) => (byte)(((uint)id & MessageNumberMask) >> MessageNumberPosition);

    /* bus numbers for request/response messages and multi-frame reports */
    public const int BusIDPosition = 16;
    public const uint BusIDMask = 0xFF << BusIDPosition;
    public static uint SetBusID(byte id) => (((uint)id << BusIDPosition) & BusIDMask);
    public static byte GetBusID(uint id) => (byte)((id & BusIDMask) >> BusIDPosition);

    public const int SourceBusIDPosition = 16;
    public const uint SourceBusIDMask = 0xF << SourceBusIDPosition;
    public static uint SetSourceBusID(byte id) => (((uint)id << SourceBusIDPosition) & SourceBusIDMask);
    public static byte GetSourceBusID(uint id) => (byte)((id & SourceBusIDMask) >> SourceBusIDPosition);
    public const int TargetBusIDPosition = 20;
    public const uint TargetBusIDMask = 0xF << TargetBusIDPosition;
    public static uint SetTargetBusID(byte id) => (((uint)id << TargetBusIDPosition) & TargetBusIDMask);
    public static byte GetTargetBusID(uint id) => (byte)((id & TargetBusIDMask) >> TargetBusIDPosition);

    /* bridge numbers for request/response messages and multi-frame reports */
    public const int BridgePosition = 16;
    public const uint BridgeMask = 0xFF << BridgePosition;
    public static uint SetBridge(byte id) => (((uint)id << BridgePosition) & BridgeMask);
    public static byte GetBridge(uint id) => (byte)(((uint)id & BridgeMask) >> BridgePosition);
    public const byte LocalBridge = 0x00;

    /* random number for address discovery messages */
    public static uint SetRandomElement(byte id) => SetBusID(id);
    public static byte GetRandomElement(uint id) => GetBusID(id);

    /* message types */
    public const int TypePosition = 24;
    public const uint TypeMask = 0x3 << TypePosition;
    public static MessageType GetType(uint id) => (MessageType)(id & TypeMask);

    /* message priorities */
    public const int PriorityPosition = 26;
    public const uint PriorityMask = 0x7 << PriorityPosition;
    public static uint SetPriority(uint prio) => (prio << PriorityPosition);
    public static uint GetPriority(uint id) => ((id & PriorityMask) >> PriorityPosition);

    /* below macros return true if the CAN ID matches the specified message type */
    //public static uint THINGSET_CAN_CONTROL(uint id) => (((id & THINGSET_CAN_TYPE_MASK) == THINGSET_CAN_TYPE_CONTROL) && THINGSET_CAN_PRIO_GET(id) < 4);
    public static bool IsReport(uint id) => (((id & TypeMask) == (uint)MessageType.SingleFrameReport) && GetPriority(id) >= 4);
    public static bool IsChannel(uint id) => ((id & TypeMask) == (uint)MessageType.RequestResponse);

    internal static uint CreateCanID(MessageType messageType, MessagePriority messagePriority, byte source, byte destination)
    {
        return CreateCanID(messageType, messagePriority, LocalBridge, source, destination);
    }

    internal static uint CreateCanID(MessageType messageType, MessagePriority messagePriority, byte bridge, byte source, byte destination)
    {
        return SocketCanUtils.CreateCanIdWithFlags((uint)messageType | (uint)messagePriority | CanID.SetBridge(bridge) | CanID.SetSource(source) | CanID.SetTarget(destination), true, false, false);
    }
}