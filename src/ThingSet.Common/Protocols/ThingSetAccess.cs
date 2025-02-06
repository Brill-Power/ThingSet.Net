/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;

namespace ThingSet.Common.Protocols;

[Flags]
public enum ThingSetAccess
{
    UserRead = 1 << 0,
    ExpertRead = 1 << 1,
    ManufacturerRead = 1 << 2,
    AnyRead = UserRead | ExpertRead | ManufacturerRead,
    UserWrite = 1 << 4,
    ExpertWrite = 1 << 5,
    ManufacturerWrite = 1 << 6,
    AnyWrite = UserWrite | ExpertWrite | ManufacturerWrite,
    AnyReadWrite = AnyRead | AnyWrite,
}
