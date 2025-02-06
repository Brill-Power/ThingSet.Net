/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using ThingSet.Common.Transports.Can;

public interface IAddressClaimListener : IDisposable
{
    event EventHandler<AddressClaimEventArgs>? AddressClaimed;

    void Listen();
}