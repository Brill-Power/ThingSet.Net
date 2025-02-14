/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Client.Schema;

/// <summary>
/// Provides an abstraction over the retrieval of a schema for a given client.
/// This can be used to implement a caching strategy for schemas.
/// </summary>
public interface IThingSetSchemaProvider
{
    ThingSetSchema GetSchema(ThingSetClient client);
}