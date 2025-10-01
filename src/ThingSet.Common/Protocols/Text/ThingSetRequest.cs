/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Protocols.Text;

public enum ThingSetRequest : byte
{
    GetFetch = (byte)'?', /**< Function code for GET and FETCH requests in text mode. */
    Exec     = (byte)'!', /**< Function code for EXEC request in text mode. */
    Delete   = (byte)'-', /**< Function code for DELETE request in text mode. */
    Create   = (byte)'+', /**< Function code for CREATE request in text mode. */
    Update   = (byte)'=', /**< Function code for UPDATE request in text mode. */
    Desire   = (byte)'@', /**< Function code for DESIRE in text mode. */
    Report   = (byte)'#', /**< Function code for REPORT in text mode. */
}