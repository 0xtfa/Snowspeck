# Snowspeck
**A lightweight, thread-safe, extensible, and decodable Snowflake ID generator for .NET.**

![NuGet](https://img.shields.io/nuget/v/Snowspeck.svg)
![License: MIT](https://img.shields.io/badge/license-MIT-yellow.svg)

---

## Features

- 64-bit unique, time-ordered ID generation
- Fully thread-safe and lock-free
- Up to 4096 IDs per millisecond per generator
- Decodable (timestamp, datacenter ID, worker ID, sequence)
- Testable: supply a `TimeProvider` to control the clock
- Trimming and Native AOT compatible
- Minimal dependencies (`Microsoft.Extensions.Options`)
- Great for distributed systems and high-throughput workloads

Supports **.NET 8** and **.NET 10** (and later).

---

## What is a Snowflake ID?

A Snowflake ID is a 64-bit integer **originally developed by Twitter** to generate unique, time-ordered identifiers in distributed systems.

It typically encodes:
- A timestamp (usually in milliseconds)
- A machine or worker ID (identifying the node that generated the ID)
- A sequence number (to prevent collisions when generating multiple IDs in the same millisecond)

This structure ensures:
- Globally unique identifiers without coordination between nodes
- IDs that increase over time (sortable)
- High-performance generation, suitable for distributed environments

### Disclaimer

Snowflake IDs are intentionally decodable and expose metadata like generation time and worker identity.  
They are **not cryptographically secure** and should **not** be used to encode sensitive or private information.

Use them only for **unique, ordered identifiers**, not for hiding or securing data.

---

## Installation

Using the CLI:

```bash
dotnet add package Snowspeck
```

Or via the NuGet Package Manager:

```powershell
Install-Package Snowspeck
```

---

## Implementations

Snowspeck offers **two implementations** of the generator, depending on whether you prefer signed or unsigned IDs:

| Class                       | Interface                   | Return Type | Lifetime from epoch | Notes                                       |
|----------------------------|-----------------------------|-------------|---------------------|---------------------------------------------|
| `SignedSnowflakeService`   | `ISnowflakeGenerator<long>`  | `long`      | ~69 years (positive) | Default in DI setup, fits in signed columns |
| `UnsignedSnowflakeService` | `ISnowflakeGenerator<ulong>` | `ulong`     | ~139 years          | Use when unsigned is OK                     |

Both use the same structure:

| Bits | Field          | Range                         |
|------|----------------|-------------------------------|
| 41   | Timestamp      | Milliseconds since the epoch  |
| 5    | Datacenter ID  | 0–31                          |
| 5    | Worker ID      | 0–31                          |
| 12   | Sequence       | 0–4095 per millisecond        |

Choose the implementation that best fits your database, serialization, or sorting needs.

> **Important:** every generator that runs at the same time must have a unique `DatacenterId`/`WorkerId` pair.
> Create **one** generator per process and reuse it (register it as a singleton). Two generators with the same
> pair, even inside the same process, can produce duplicate IDs.

---

## Quick Start

### Manual Instantiation

```csharp
using Snowspeck;
using Snowspeck.Interfaces;
using Snowspeck.Services;

var options = new SnowflakeOptions
{
    DatacenterId = 1, // 0–31
    WorkerId = 1,     // 0–31, unique per datacenter
    Epoch = 1735689600000L // (optional) Default is 1735689600000L -> Jan 1, 2025
};

ISnowflakeGenerator<long> generator = new SignedSnowflakeService(options);
long id = generator.NextId();
```

---

### Dependency Injection (ASP.NET Core)

```csharp
using Snowspeck.Extensions;

builder.Services.AddSnowspeck(options =>
{
    options.WorkerId = 1;
    options.Epoch = 1735689600000L; // (optional) Default is 1735689600000L -> Jan 1, 2025
});
```

```csharp
builder.Services.AddSnowspeckUnsigned(options =>
{
    options.WorkerId = 1;
    options.Epoch = 1735689600000L; // (optional) Default is 1735689600000L -> Jan 1, 2025
});
```

Once registered, resolve `ISnowflakeGenerator<long>` (or `ISnowflakeGenerator<ulong>` for the unsigned variant) via constructor injection:

```csharp
public class MyService
{
    private readonly ISnowflakeGenerator<long> _generator;

    public MyService(ISnowflakeGenerator<long> generator)
    {
        _generator = generator;
    }

    public void DoSomething()
    {
        long id = _generator.NextId();
    }
}
```

---

### Decoding Snowflake Metadata

The generator supports decoding a Snowflake ID back into:

- `Timestamp` (Unix time in milliseconds when the ID was generated)
- `DatacenterId`
- `WorkerId`
- `Sequence`

```csharp
long id = generator.NextId();
var metadata = generator.Decode(id);

Console.WriteLine($"Timestamp: {metadata.Timestamp}");
Console.WriteLine($"Created at: {metadata.ToDateTimeOffset():u}");
Console.WriteLine($"DatacenterId: {metadata.DatacenterId}");
Console.WriteLine($"WorkerId: {metadata.WorkerId}");
Console.WriteLine($"Sequence: {metadata.Sequence}");
```

---

## Clock Handling

Snowflake IDs rely on the system clock never going backwards. If it does (for example after an NTP correction):

- If it moves back by **at most** `MaxClockSkewMs` (default 10 ms), the generator waits for the clock to catch up.
- If it moves back by more, `NextId()` throws a `SnowflakeClockException` instead of risking duplicate IDs.
  The generator keeps working once the clock is correct again.

```csharp
builder.Services.AddSnowspeck(options =>
{
    options.WorkerId = 1;
    options.MaxClockSkewMs = 50; // tolerate up to 50 ms of backward clock movement
});
```

The epoch must not be in the future and must never change once IDs have been issued.

---

## Using IDs in JavaScript / JSON

Snowflake IDs are larger than JavaScript's `Number.MAX_SAFE_INTEGER` (2⁵³−1), so JavaScript clients would round them.
Send them as strings instead, for example with `System.Text.Json`:

```csharp
using System.Text.Json.Serialization;

public class Order
{
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public long Id { get; set; }
}
```

---

## Testing

To control the timestamps a generator produces in tests, pass a `TimeProvider`, for example
`FakeTimeProvider` from the `Microsoft.Extensions.TimeProvider.Testing` package:

```csharp
var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
var generator = new SignedSnowflakeService(new SnowflakeOptions { WorkerId = 1 }, clock);

long id = generator.NextId();
generator.Decode(id).ToDateTimeOffset(); // 2026-01-01T00:00:00Z
```

A generator issues at most 4096 IDs per millisecond, so advance a fake clock (for example with
`clock.Advance(...)` or `clock.AutoAdvanceAmount`) when a test needs more than that.

The `AddSnowspeck` registrations always use the system clock, even when a `TimeProvider` is registered in the container.
To use a custom clock with dependency injection, register the generator yourself:

```csharp
builder.Services.AddSingleton<ISnowflakeGenerator<long>>(sp => new SignedSnowflakeService(
    new SnowflakeOptions { WorkerId = 1 },
    sp.GetRequiredService<TimeProvider>()));
```

---

## Extensibility

`SignedSnowflakeService` and `UnsignedSnowflakeService` are built on `SnowflakeServiceBase<TId>`. Derive from it to
change how IDs are composed and parsed, where the time comes from (`GetCurrentTimestamp`), or how clock skew is
handled (`HandleClockSkew`, `WaitForNextMillisOffset`).

### Have an idea or feature request? Feel free to open an issue.

---

## License

Licensed under the [MIT License](LICENSE).  
© 2025 Tiago Ferreira Alves
