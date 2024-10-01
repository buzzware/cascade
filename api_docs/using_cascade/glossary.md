@page glossary Glossary

### Freshness

How recently did a data record arrive from the server? The value is represented in milliseconds and is inverse ie a value 
of 0 means the most fresh.

### Fallback Freshness

A value of acceptable freshness applied to the situation where ConnectionOnline == True and a request to the origin has failed due 
to network or server failure. In that situation, if the required value is in cache and is fresher (more recent than) than the 
fallback freshness value, it will be returned otherwise ignored.

### Data Properties

Properties of a model that hold data

### Association Properties

Properties of a model that hold a reference or array of references to other models, or the result of a blob conversion (using `[FromBlob]`).
These properties are not serialized or deserialized as they are not recognised by the origin.
For example :

```
public class Child {



}






```
