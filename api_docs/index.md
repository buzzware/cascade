# Cascade Data Layer: A Framework for Mobile App Data Flow

Cascade is a framework designed to optimize data flow between the user interface and the server, while
incorporating caching and persistence mechanisms. Acting as a backend
for the front end, Cascade offers several valuable benefits that enhance application efficiency and maintain
consistency. By providing a well-defined API with clear relationships,
Cascade enables developers to write the user interface in a consistent and meaningful manner.

The architecture of Cascade is inspired by the concept of cache layers in CPUs. Unlike CPUs, Cascade 
supports 0 or more abstract cache layers.   
The initial first layer is usually memory based - fastest but limited and not persistent. The second is typically 
file-based - slower, but persistent. Cache layers are abstract, providing developers with the flexibility to 
implement their preferred caching storage.

Cascade also requires an implementation of an abstract origin (server) interface. This means almost any server can 
be supported, including custom enterprise APIs. Inconsistencies and naming issues can be resolved in custom code to 
maintain a clean and consistent API to app developers. By providing a robust data layer, Cascade shields the front 
end from any changes or updates made to the backend. 

### Usage
 
As a quick introduction, the main methods provided by Cascade for applications are :

1. ```var product = await cascade.Create<Product>(new Product() { colour = "Red" });```
1. ```var product = await cascade.Get<Product>(25, populate: new string[] { nameof(Product.Manufacturer) });```
2. ```var redThings = await cascade.Query<Product>("red_products",new JsonObject { ["colour"] = "red" });```
3. ```var updated = await cascade.Update(product, new JsonObject { ["colour"] = "red" });```
4. ```await cascade.Destroy(product);```
5. ```var promoted = await cascade.Execute("PROMOTE",new JsonObject { ["product_id"] = 25 })```
5. ```await cascade.Populate(product,new string[] { nameof(Product.Manufacturer),nameof(Product.Category) })```

### Application Requirements :

1. Using the SuperModel base class and its attributes for all application models
2. Implementing an origin class for your server(s) API
3. Constructing an instance of CascadeDataLayer with the desired cache layer(s) and origin
4. Using the CascadeDataLayer for application server interactions
5. As for any application that needs to create records offline, you should probably use GUID ids

### Features

1. "freshness" option to determine whether to get data from either a cache or the server
2. "fallback freshness" option to silently fallback to a cached data when unable to reach the server
3. "hold" option to mark retrieved records for downloading and preservation offline even when caches are cleared  
