# ThingSet.Net

.NET client implementation of the ThingSet protocol. Supported transports are CAN and IP. At present, only the binary wire format (CBOR) is supported.

For more information on ThingSet, see [the ThingSet web site](https://thingset.io).

## Installation

NuGet packages are available. Install [the client](https://www.nuget.org/packages/ThingSet.Client/) and
the separate package for whichever transport you require:

* for IP (TCP/UDP), [ThingSet.Common.Transports.Ip](https://www.nuget.org/packages/ThingSet.Common.Transports.Ip)
* for CAN (on Linux), [ThingSet.Common.Transports.Can](https://www.nuget.org/packages/ThingSet.Common.Transports.Can)

## Quick Start

### Create a transport and client

```csharp
IpClientTransport ipTransport = new IpClientTransport("mydevice", 9001);
ThingSetClient client = new ThingSetClient(ipTransport);
await client.ConnectAsync();
```

### Get values and invoke functions

```csharp
object? value = client.Get(0x100);

object? result = client.Exec(0x300, 1, 2);
```

### Subscribe for reports

```csharp
await client.SubscribeAsync((uint id, string? path, object? value) =>
{
    Console.WriteLine($"Report: {path} ({id:x}): {value?.ToString()}");
});
```
