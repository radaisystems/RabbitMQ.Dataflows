# RabbitDataflows

![TesseractLogo](https://raw.githubusercontent.com/houseofcat/RabbitDataflows/main/TesseractLogo.svg)

A library of `NetCore` tools to help rapidly develop well performant micro/macroservices. 

Prototypes you could send to production!  

## Why Make A RabbitMQ Powered Dataflow  

`Dataflows` have concurrency, serialization, monitoring, compression, and encryption all as first class citizens. This paradigm allows developers to just focus on the important stuff - getting work done. Dataflows pay attention to the extra dimensions so you don't have to!

Here are some features ready with RabbitMQ today, tomorrow - the world!

### Queueing
* Async Processing    
* Retriability  
* Chaos Engineering  
* Connection/Channel Durability provided by `HouseofCat.RabbitMQ` formerly `CookedRabbit.Core`.  

### Built-Ins
* Supports `ILogger<T>`  
* Concurrency/Parallelism - baked in from the ground up.  
* Contracted `WorkState`/WorkObject simplifies development and integration.  
* Has `Json` (3 flavors) and `MessagePack` serialization providers.
* Allow transparent encryption/decryption steps.  
* Allow compression/decompression steps to reduce trip time over the wire.  
* Async Error Handling with Predicate triggers and an actionable callback.  

### Interchangeability
* Allows you to replace serialization provider with `HouseofCat` Provider wrappers.  
* Allows you to replace encryption provider with `HouseofCat` Provider wrappers.  
* Allows you to replace compression provider with `HouseofCat` Provider wrappers.   
* Constructed to fully support Inversion of Control.  

### Business Logic
* All steps process in the order provided allowing you to still control order of execution.  
* All automatically subscribed to Async Error handling by `WorkState.IsFaulted` flag.  

### Testing
* All built-in steps will have integration tests that should remove concerns from end-user developer.   
* Future case will include much more complex abstract UnitTesting as time allows.  
* The developer should only need to unit test their functional business code.  

## Implicit Benefits

The benefits of a dataflow pattern extend beyond fancy machine learning and Tensorflows or high throughput GCP Dataflow for mass computation. When brought to the service level, it helps organize your code into more manageable blocks. You can still write monolithic functions, but you would be hamstringing yourself and scarificing concurrency and parallelism. By designing code into small functional steps, you always write better, cleaner, code reduced with cyclomatic complexity. That very same code is easier to UnitTest. The orchestration of the function calls are the order they are added allowing you extend the original functionality infinitely. You don't have to write deserialization or post-processing encryption/compression as they all baked in. Designing from the ground up with concurrency and parallelism, you stay nimble and fast - able to scale up internally, before horizontally and vertically, saving costs. All without needing code changed or refactored.

Lastly, after everything is said and done, all your business code is re-usable. Should you decide to abandon this workflow (:worried:) for a different mechanism, engine, or what not, all of your code will happily port to whatever other project / flow you are working with and so will all your testing making it a win win.

## Help
You will find library usage examples in the `examples` folder. You also can find generic NetCore how-tos and tutorials located in there. The code quality of the entire library will improve over time. Codacy allows me to review code and openly share any pain points so submit a PR to help keep this an A rated library!

Check out each project for additional `README.md`. They will provide additional instructions/examples.

## Status

[![Codacy Badge](https://api.codacy.com/project/badge/Grade/9dbb20a30ada48caae4b92a83628f45e)](https://app.codacy.com/gh/houseofcat/RabbitDataflows/dashboard)  

[![build](https://github.com/houseofcat/HouseofCat.Library/workflows/build/badge.svg)](https://github.com/houseofcat/RabbitDataflows/actions/workflows/build.yml)

[![Gitter](https://badges.gitter.im/HoC-Tesseract/community.svg)](https://gitter.im/HoC-Tesseract/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

# Main RabbitMQ Libraries

## HouseofCat.RabbitMQ
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.RabbitMQ.svg)](https://www.nuget.org/packages/HouseofCat.RabbitMQ/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.RabbitMQ.svg)](https://www.nuget.org/packages/HouseofCat.RabbitMQ/)  

A library that focuses on RabbitMQ connection and channel management to create fault tolerant Publishers and Consumers.  
Formerly called CookedRabbit.Core/Tesseract.

## HouseofCat.RabbitMQ.Dataflows
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.RabbitMQ.Dataflows.svg)](https://www.nuget.org/packages/HouseofCat.RabbitMQ.Dataflows/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.RabbitMQ.Dataflows.svg)](https://www.nuget.org/packages/HouseofCat.RabbitMQ.Dataflows/)  

A library that extends HouseofCat.RabbitMQ functionality by providing epic TPL Dataflow usage for Tesseract.  

## HouseofCat.RabbitMQ.Pipelines
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.RabbitMQ.Pipelines.svg)](https://www.nuget.org/packages/HouseofCat.RabbitMQ.Pipelines/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.RabbitMQ.Pipelines.svg)](https://www.nuget.org/packages/HouseofCat.RabbitMQ.Pipelines/)  

A library that extends HouseofCat.RabbitMQ functionality by providing simplified TPL Dataflow usage called Pipelines.  

## HouseofCat.RabbitMQ.Services
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.RabbitMQ.Services.svg)](https://www.nuget.org/packages/HouseofCat.RabbitMQ.Services/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.RabbitMQ.Services.svg)](https://www.nuget.org/packages/HouseofCat.RabbitMQ.Services/)  

A library that extends HouseofCat.RabbitMQ to simplify using the HouseofCat.RabbitMQ library. Recommend using this for beginners.  

## HouseofCat.RabbitMQ.WorkState
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.RabbitMQ.WorkState.svg)](https://www.nuget.org/packages/HouseofCat.RabbitMQ.WorkState/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.RabbitMQ.WorkState.svg)](https://www.nuget.org/packages/HouseofCat.RabbitMQ.WorkState/)  

A library that creates a shareable WorkState class for HouseofCat.RabbitMQ.

# DataFlow Libraries

## HouseofCat.Dataflows
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Dataflows.svg)](https://www.nuget.org/packages/HouseofCat.Dataflows/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Dataflows.svg)](https://www.nuget.org/packages/HouseofCat.Dataflows/)  

A library that provides the base magic Dataflows for RabbitDataflows. 

## HouseofCat.Dataflows.Pipelines
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Dataflows.Pipelines.svg)](https://www.nuget.org/packages/HouseofCat.Dataflows.Pipelines/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Dataflows.Pipelines.svg)](https://www.nuget.org/packages/HouseofCat.Dataflows.Pipelines/)  

A library that provides the base magic Pipelines for RabbitDataflows. 

# Core Productivity Libraries
These libraries are here to help you build powerful Dataflows for your messages.

## HouseofCat.Logger
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Logger.svg)](https://www.nuget.org/packages/HouseofCat.Logger/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Logger.svg)](https://www.nuget.org/packages/HouseofCat.Logger/)  

A library that focuses on getting Microsoft.Extensions.LoggerFactory implemented adhoc globally since Dependency Injection with
the Factory/Builder pattern can be difficult to maintain. 

## HouseofCat.Compression
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Compression.svg)](https://www.nuget.org/packages/HouseofCat.Compression/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Compression.svg)](https://www.nuget.org/packages/HouseofCat.Compression/)  

A library that has a collection of builtin NetCore compression providers.  

## HouseofCat.Compression.LZ4
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Compression.LZ4.svg)](https://www.nuget.org/packages/HouseofCat.Compression.LZ4/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Compression.LZ4.svg)](https://www.nuget.org/packages/HouseofCat.Compression.LZ4/)  

A library that focuses on implementing the LZ4 compression provider.  

## HouseofCat.Compression.Recyclable
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Compression.Recyclable.svg)](https://www.nuget.org/packages/HouseofCat.Compression.Recyclable/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Compression.Recyclable.svg)](https://www.nuget.org/packages/HouseofCat.Compression.Recyclable/)  

A library that has a collection of builtin NetCore compression providers that uses object pools and RecyclableMemoryStreams. 

## HouseofCat.Data.Recyclable
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Data.Recyclable.svg)](https://www.nuget.org/packages/HouseofCat.Data.Recyclable/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Data.Recyclable.svg)](https://www.nuget.org/packages/HouseofCat.Data.Recyclable/)  

A library that provides the provides helper classes for data manipulation and transformation that uses object pooling and RecyclableMemoryStreams.

## HouseofCat.Hashing
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Hashing.svg)](https://www.nuget.org/packages/HouseofCat.Hashing/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Hashing.svg)](https://www.nuget.org/packages/HouseofCat.Hashing/)  

A library that focuses on implementing hashing.  

## HouseofCat.Hashing.Argon
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Hashing.Argon.svg)](https://www.nuget.org/packages/HouseofCat.Hashing.Argon/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Hashing.Argon.svg)](https://www.nuget.org/packages/HouseofCat.Hashing.Argon/)  

A library that focuses on implementing Argon2ID hashing.  

## HouseofCat.Encryption.Providers
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Encryption.Providers.svg)](https://www.nuget.org/packages/HouseofCat.Encryption.Providers/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Encryption.Providers.svg)](https://www.nuget.org/packages/HouseofCat.Encryption.Providers/)  

A library that provides encryption contracts and base AesGCM/AesCBC NetCore encryption providers. 

## HouseofCat.Encryption.BouncyCastle
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Encryption.BouncyCastle.svg)](https://www.nuget.org/packages/HouseofCat.Encryption.BouncyCastle/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Encryption.BouncyCastle.svg)](https://www.nuget.org/packages/HouseofCat.Encryption.BouncyCastle/)  

A library that provides encryption from the BouncyCastle provider.  

## HouseofCat.Encryption.Recyclable
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Encryption.Recyclable.svg)](https://www.nuget.org/packages/HouseofCat.Encryption.Recyclable/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Encryption.Recyclable.svg)](https://www.nuget.org/packages/HouseofCat.Encryption.Recyclable/)  

A library that provides encryption from base AesGcm class in NetCore but with ArrayPools and RecyclableMemoryStreams.

# Non-Critical Library Integrations

## HouseofCat.Dapper
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Dapper.svg)](https://www.nuget.org/packages/HouseofCat.Dapper/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Dapper.svg)](https://www.nuget.org/packages/HouseofCat.Dapper/)  

A library that provides simplifications for rapidly prototyping with Dapper.

## HouseofCat.Data
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Data.svg)](https://www.nuget.org/packages/HouseofCat.Data/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Data.svg)](https://www.nuget.org/packages/HouseofCat.Data/)  

A library that provides the provides helper classes for data manipulation and transformation. 

## HouseofCat.Data.Database
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Data.Database.svg)](https://www.nuget.org/packages/HouseofCat.Data.Database/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Data.Database.svg)](https://www.nuget.org/packages/HouseofCat.Data.Database/)  

A library that provides the provides a Database Connection Factory and Sql Generation.

### Database Connection Factory Support
 * System.Data.SqlClient
 * Microsoft.Data.SqlClient
 * MySql.Data.MySqlClient
 * Npgsq
 * MySql.Data
 * Oracle
 * SQLite

### Sql Query Generation
 * SqlServer
 * MySql
 * PostgreSql
 * Firebird
 * Oracle
 * SQLite

## HouseofCat.Serialization
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Serialization.svg)](https://www.nuget.org/packages/HouseofCat.Serialization/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Serialization.svg)](https://www.nuget.org/packages/HouseofCat.Serialization/)  

A library that focuses on making it easier to deal with Serialization.  

## HouseofCat.Serialization.Json.Newtonsoft
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Serialization.Json.Newtonsoft.svg)](https://www.nuget.org/packages/HouseofCat.Serialization.Json.Newtonsoft/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Serialization.Json.Newtonsoft.svg)](https://www.nuget.org/packages/HouseofCat.Serialization.Json.Newtonsoft/)  

A library that focuses on making it easier to deal with Newtonsoft Json Serialization.  

## HouseofCat.Serialization.MessagePack
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Serialization.MessagePack.svg)](https://www.nuget.org/packages/HouseofCat.Serialization.MessagePack/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Serialization.MessagePack.svg)](https://www.nuget.org/packages/HouseofCat.Serialization.MessagePack/)  

A library that focuses on making it easier to deal with MessagePack Serialization.  

## HouseofCat.Recyclable
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Recyclable.svg)](https://www.nuget.org/packages/HouseofCat.Recyclable/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Recyclable.svg)](https://www.nuget.org/packages/HouseofCat.Recyclable/)  

A library that focuses on Recyclable classes and pooling. 

## HouseofCat.Reflection
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Reflection.svg)](https://www.nuget.org/packages/HouseofCat.Reflection/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Reflection.svg)](https://www.nuget.org/packages/HouseofCat.Reflection/)  

A library that focuses on Reflection hackery.  

## HouseofCat.Utilities
[![NuGet](https://img.shields.io/nuget/v/HouseofCat.Utilities.svg)](https://www.nuget.org/packages/HouseofCat.Utilities/)  
[![NuGet](https://img.shields.io/nuget/dt/HouseofCat.Utilities.svg)](https://www.nuget.org/packages/HouseofCat.Utilities/)  

A library that focuses on general purpose utilities and functions that simplify the coding experience.  

## Example Integration Project Ideas

### HouseofCat.RabbitMQ.Twilio
An example project/library that extends HouseofCat.RabbitMQ.Services to simplify using Twilio (SMS/TextMessages) with the HouseofCat.RabbitMQ library. 

### HouseofCat.RabbitMQ.Mailkit
An example project/library that extends HouseofCat.RabbitMQ.Services to simplify using Mailkit (Email) with the HouseofCat.RabbitMQ library. 

## Extra Side Projects

### HouseofCat.Algorithms
An example project/library that has a collection of algorithms as I have time to learn and play with them.  

### HouseofCat.Data.Parquet
An example project/library that helps extract databases to parquet file (IDataReader -> Snappy compressed Parquet files.)

### HouseofCat.Framing
An example project/library that focuses on simplifying reading groups of byte[] (designated as frames).  

### HouseofCat.Gremlins
An example project/library that focuses on Chaos Engineering. Currently targets Windows OS.  

### HouseofCat.Gremlins.SqlServer
An example project/library that focuses on Chaos Engineering with SqlServer. Currently targets System.Data.SqlClient.  

### HouseofCat.Network
An example project/library that focuses on making it easier to deal with systems networking.  

### HouseofCat.Serialization.Json.Utf8Json
An example project/library that focuses on making it easier to deal with Utf8Json (cysharp/neuecc) Json Serialization.  

### HouseofCat.Serilog
An example project/library that focuses on extending IHost functionality to quickly setup Serilog.  

### HouseofCat.Sockets
An example project/library that focuses on making it easier to deal with socket communication.  

### HouseofCat.Sockets.Utf8Json
An example project/library that focuses on making it easier to deal with sockets communication with Utf8Json.  

### HouseofCat.Windows.Keyboard
An example project/library that focuses on interacting, filtering, and/or replaying user inputs on Windows, specifically Keyboard.  

### HouseofCat.Windows.NativeMethods
An example project/library that focuses on consolidating Windows NativeMethods calls used by my libaries.  

### HouseofCat.Windows.Threading
An example project/library that focuses on simplifying affinity and thread management.  

### HouseofCat.Windows.WMI
An example project/library that focuses on performing System.Management (Windows.Compatibility.Pack) WMI Queries.  

# [HouseofCat.io](https://houseofcat.io)
