/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Generic;
using ThingSet.Client.Schema;

namespace ThingSet.Client;

public interface IThingSetClient : IDisposable
{
    object? Exec(uint id, params object[] args);

    object? Exec(string path, params object[] args);

    object? Update(string fullyQualifiedName, object value);

    object? Get(uint id);

    object? Get(string path);

    IEnumerable<ThingSetNode> GetNodes(ThingSetNodeEnumerationOptions options);
}