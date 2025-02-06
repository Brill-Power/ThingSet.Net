/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Protocols;

/// <summary>
/// Represents a response received from a ThingSet device.
/// </summary>
public struct ThingSetResponse
{
    public ThingSetResponse(ThingSetStatus status) : this(status, new byte[0])
    {
    }

    public ThingSetResponse(ThingSetStatus status, byte[] buffer)
    {
        Status = status;
        Buffer = buffer;
    }


    /// <summary>
    /// Gets the status code returned in the response.
    /// </summary>
    public ThingSetStatus Status { get; }

    public byte[] Buffer { get; }

    /// <summary>
    /// If true, the response indicates that the request that preceeded it
    /// was successful.
    /// </summary>
    public bool Success => (int)Status <= 0x9f;

    public static implicit operator ThingSetResponse(int zero) => new ThingSetResponse((ThingSetStatus)0);

    public static implicit operator ThingSetResponse(ThingSetStatus status) => new ThingSetResponse(status);

    public static implicit operator ThingSetStatus(ThingSetResponse self) => self.Status;

    public static implicit operator bool(ThingSetResponse self) => self.Success;
}