/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Client.Schema;

public interface IThingSetSchemaProvider
{
    ThingSetSchema GetSchema(ThingSetClient client);
}