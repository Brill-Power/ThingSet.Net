/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Client.Schema;

public class DefaultThingSetSchemaProvider : IThingSetSchemaProvider
{
    public ThingSetSchema GetSchema(ThingSetClient client)
    {
        return new ThingSetSchema(client);
    }
}