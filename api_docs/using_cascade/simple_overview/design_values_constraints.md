@page values_and_constraints Design Values and Constraints 

# Cascade Design Values and Constraints


## Values

1. Explicitness
2. Developer Productivity
3. Familiarity
4. Transparency - it is better to require the developer to be more involved in details than to build abstractions that don't work in every scenario  
5. No "magic" - when magic doesn't work, it is very difficult to do anything about it since you didn't know what it was doing in the first place
6. Providing both extremes (simple and full featured) is better than providing a single compromise method
7. Keep models simple and not asynchronous - 

## Constraints

1. Must work on Xamarin Forms, MAUI, Avalonia and Uno
2. Must be compatible with XAML property binding
3. Models are normally immutable to app code, except for the mutable proxy pattern eg. for forms

## In Scope

* Caching
* Persistence
* Offline
* Queries
* Taking queries offline

## Out of Scope

* local queries - queries only happen in the ICascadeOrigin (which can be implemented to do queries anywhere) 
* automatic refreshing of collections when models change - use HasMany* maintenance methods
* many-to-many relationships - use a join table with HasMany and BelongsTo. You'll likely add properties to the join table anyway
* Reactivity and Subscription (for now) - ie changes on the server being pushed to the client
