# cascade
Cascade Data Layer - data management layer for C# client applications

Under active development September 2022

## Features

* Arbitrary number of cache layers for performance, reducing server load and offline support 
* Designed to support offline for seconds to long term, including writes (offline write not yet implemented)
* Abstract origin (server) and cache layers for supporting any existing server or data store
* One Way Dataflow (from Facebook)
* Provides SuperModel base class for binding, associations, sometimes immutability, mutable proxy
