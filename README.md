# Cascade Data Layer 
## Data management framework for C# client (mobile) applications

Cascade is a solution to several problems of data management within front end client apps. It provides a clean, consistent, and familiar API that assists the developer in performing data operations with the server, and maintaining data structures consisting of models and collections with the results.

Cascade is designed to work well with existing dotnet UI frameworks, with or without binding. Cascade is also designed to work with virtually any backend server or store, including multiple, by custom implementation of abstract interfaces.

By following the patterns made easy with Cascade methods, an application gets caching, local data persistence and offline-online network resilience almost for free.

An application built on Cascade uses custom model classes subclassing the provided SuperModel class, and calls methods on a CascadeDataLayer class instance for all operations. Serialisation/deserialisation happens mostly automatically using System.Json.Text but the application code only operates with models. Building an application on this layer means isolation from server implementation details and changes. Testing can be easily performed on application code using a mock server implementation.

### Benefits

1. A clean and simple interface, reminiscent of a HTTP client, for application server interactions
2. True offline support for all operations
3. You send and receive models, not JSON

5. easy Get/Query of models with their associations via Populate method/parameter
6. seamless multilayer caching & persistence, including collections and queries
7. Optional memory caching for speed
8. Optional file system caching/persistence
9. Supports almost any API server(s) through your own implementation of abstract interfaces
10. Insulation of application logic from server irregularities and changes

### Features

1. Mostly Immutable Models (application code should never need to modify models directly) 
2. Associations (Relations) between models: BelongsTo (Many to One), HasMany (One to Many), HasOne (One To One)
3. Pagination including infinite scroll for queries
3. Multithreaded internally for performance
4. Threadsafe for use in alternative threads, including bindable UI properties only modified on the main thread
4. Support for binary blobs eg. images (including caching and offline)
4. Support for meta-data about models and blobs
2. "freshness" option to determine whether to get data from either a cache or the server
2. "fallback freshness" option to silently fallback to a cached data when unable to reach the server
3. "hold" option to mark retrieved records for downloading and preservation offline even when caches are cleared 


[API Reference](https://buzzware.github.io/cascade/)

[Overview Presentation Slides](https://buzzware.github.io/cascade/presentation)



### Usage

#### Examples of main methods

1. ```var product = await cascade.Create<Product>(new Product() { colour = "Red" });```
1. ```var product = await cascade.Get<Product>(25, populate: new string[] { nameof(Product.Manufacturer) });```
2. ```var redThings = await cascade.Query<Product>("red_products",new JsonObject { ["colour"] = "red" });```
3. ```var updated = await cascade.Update(product, new JsonObject { ["colour"] = "red" });```
4. ```await cascade.Destroy(product);```
5. ```var promoted = await cascade.Execute("PROMOTE",new JsonObject { ["product_id"] = 25 })```
5. ```await cascade.Populate(product,new string[] { nameof(Product.Manufacturer),nameof(Product.Category) })```

#### Cascade requires the following from your application (for all models that use Cascade) 

1. Inherit from the SuperModel base class and use the given GetProperty/SetProperty for its attributes 
2. Models should be treated as immutable by application code - attempting to set a property throws an exception. All changes are done using Cascade methods (which are propagated through the caches and origin server). 
3. Implement ICascadeOrigin with an origin class for your server(s)' API
4. Construct an instance of CascadeDataLayer with the desired cache layer(s) and origin  
5. Use the methods of CascadeDataLayer (Create/Get/Query/Update/Replace/Destroy) for all operations with those models 

