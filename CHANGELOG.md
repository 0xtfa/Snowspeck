# Changelog

All notable changes to Snowspeck are documented here. This project follows [Semantic Versioning](https://semver.org/).

## 1.1.0 (unreleased)

A drop-in update: no breaking changes. IDs keep exactly the same bit layout, so IDs created by 1.0.x decode identically,
and IDs keep increasing across the upgrade. The public API is checked against 1.0.2 on every build.

### Fixed

- **Up to ~14× higher throughput on Windows under load.** When more than 4096 IDs were requested within one millisecond,
  the generator waited for the next millisecond with `Thread.Sleep(1)`, which on Windows typically blocks for about
  15 ms. Measured on Windows: ~300 IDs/ms before, ~4,090 IDs/ms (the design limit of 4096) after. Linux and macOS were
  affected much less, because `Thread.Sleep(1)` there is close to 1 ms.
- `NextId()` could block for as long as the clock stayed behind if the clock jumped backwards while the generator was
  already waiting for the next millisecond. It now throws `SnowflakeClockException` when that jump exceeds
  `MaxClockSkewMs`, as the option is documented to do.
- When the current time is earlier than `SnowflakeOptions.Epoch` (for example, an epoch in seconds or ticks instead of
  milliseconds), the `SnowflakeClockException` message now says so instead of reporting a backward clock jump.
- The `IOptions<SnowflakeOptions>` constructors throw `ArgumentNullException` instead of `NullReferenceException`
  when passed `null`.

### Added

- Targets .NET 10 in addition to .NET 8.
- `SignedSnowflakeService(SnowflakeOptions, TimeProvider)` and `UnsignedSnowflakeService(SnowflakeOptions, TimeProvider)`
  constructors (and a matching protected constructor on `SnowflakeServiceBase<TId>`) to control the clock in tests.
  The dependency injection registrations are unchanged and keep using the system clock.
- IntelliSense documentation is now included in the package.
- Source Link and a symbol package (`.snupkg`), so you can step into Snowspeck while debugging.
- The library is marked as trimming and Native AOT compatible.

### Changed

- Documentation fixes: README samples now compile (`ISnowflakeGenerator<long>` instead of the non-existent
  `ISnowflakeGenerator`) and cover clock handling, JavaScript/JSON precision, testing and extensibility.
- The minimum `Microsoft.Extensions.Options` dependency is still 8.0.0, so upgrading Snowspeck never forces other
  package upgrades.

## 1.0.2

- `SnowflakeOptions` properties have public setters (instead of `init`), so the options can be configured through
  `AddSnowspeck(options => ...)`.

## 1.0.1 and 1.0.0

- Initial releases.
