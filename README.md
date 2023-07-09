# Cascade Data Layer 
## Data management framework for C# client (mobile) applications

### Usage

The main methods used by an application built with Cascade :

1. ```var product = await cascade.Create<Product>(new Product() { colour = "Red" });```
1. ```var product = await cascade.Get<Product>(25, populate: new string[] { nameof(Product.Manufacturer) });```
2. ```var redThings = await cascade.Query<Product>("red_products",new JsonObject { ["colour"] = "red" });```
3. ```var updated = await cascade.Update(product, new JsonObject { ["colour"] = "red" });```
4. ```await cascade.Destroy(product);```
5. ```var promoted = await cascade.Execute("PROMOTE",new JsonObject { ["product_id"] = 25 })```
5. ```await cascade.Populate(product,new string[] { nameof(Product.Manufacturer),nameof(Product.Category) })```

Using Cascade in an application means : 

1. Using the SuperModel base class and its attributes for all application models
2. Implementing an origin class for your server(s) API
3. Constructing an instance of CascadeDataLayer with the desired cache layer(s) and origin  
4. Using the CascadeDataLayer for application server interactions

### Benefits

1. A clean and simple interface, reminiscent of a HTTP client, for application server interactions
2. True offline support for all operations
3. You send and receive models, not JSONElements
4. Associations between models: BelongsTo, HasMany, HasOne 
5. easy Get/Query of models with their associations via Populate method/parameter
6. seamless multilayer caching & persistence, including collections and queries 
7. Optional memory caching for speed
8. Optional file system caching/persistence
9. Supports almost any API server(s) through your own implementation of abstract interfaces
10. Insulation of application logic from server irregularities and changes 

### Features

1. "freshness" option to determine whether to get data from either a cache or the server
2. "fallback freshness" option to silently fallback to a cached data when unable to reach the server
3. "hold" option to mark retrieved records for downloading and preservation offline even when caches are cleared  
