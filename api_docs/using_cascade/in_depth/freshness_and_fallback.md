@page freshness_and_fallback Freshness, Fallback and Time

# Freshness, Fallback and Time

The combination of freshness and fallback freshness enables applications to specify their requirements
in both typical online, online with network failure, and offline scenarios.

## Time and Now

CascadeDataLayer has a NowMs property to provide a consistent and controlled reference for the present time. All internal and related Cascade code that needs to know 
the present time should use NowMs.
This time is a 64 bit long in milliseconds since 1/1/1970 00:00:00 UTC (it is too big for 32 bits)
This time actually comes from the provided ICascadeOrigin implementation NowMs property.
In production it would probably be calculated from DateTime.Now.
In testing it can be a simple writable long property. This means that Cascade's concept of time can be controlled in unit tests.

## What is Freshness?

Freshness is a term used in cache validation. It refers to how recently a piece of data was retrieved from its original
source or "origin" in Cascade. Applications specify the freshness of the data they require when making requests. Depending upon
these requirements, a cached data entry may or may not be considered 'fresh enough' to be returned. This is determined based on the
time of arrival of each record from the origin which is subtracted from the current time, and compared with the freshness requirement. 

Freshness is only a parameter for **read** requests ie Get, Query, Populate, GetCollection or BlobGet requests.

In Cascade, freshness is expressed in integer seconds, and refers to how recently the data arrived from the origin.
It is **inverse** ie. a zero value means absolutely fresh - immediately from the origin, 
while a high value arrived long ago and is more stale.
When application code requests data from Cascade with a higher freshness value it means the application is willing to accept older data, 
and will likely use the cache more often and perform less origin requests; while a lower requirement insists on more recent data.

## What is Fallback Freshness?

In Cascade "fallback" means to return a cached value when an attempt to get the value from the origin fails. This means an app can continue without alerting the user or prompting them to retry or cancel the action they were performing.

Fallback Freshness is a freshness value that is only used when 

1. ConnectionOnline==False or 
2. ConnectionOnline==True and a NoNetworkException is thrown by the origin

> NoNetworkException is used to indicate that the mobile device was unable to connect to the origin/server for reasons such as the device being out of range, 
> or in "Airplane Mode" or some other network failure or the server is offline. It should not be used in scenarios such as the authentication or authorisation failure.   
> NoNetworkException comes from the StandardExceptions library included in Cascade package. Like all StandardExceptions, it can wrap more specific platform exceptions
> using its Inner property. 
> It is good practice to catch the various network failure exceptions of DotNet and the mobile platform and `throw new NoNetworkException(aInnerException: exception)`      

Fallback Freshness is only a parameter for **read** requests ie Get, Query, Populate, GetCollection or BlobGet requests.


## Freshness and Fallback together

The combination of freshnessSeconds and fallbackFreshnessSeconds parameters means that most application data requirements can be met in online, semi-online and offline scenarios. An application can specify how fresh it would like requested data to be when it has a network connection; and also how old it will tolerate data when offline or the network connection fails.

| Scenario                                                       | ConnectionOnline==True          | ConnectionOnline==False          |
|----------------------------------------------------------------|---------------------------------|----------------------------------|
| Each cache: no value exists                                    | => try next cache               | => try next cache                |
| Each cache: cache value freshness <= request freshness         | => return cache value           | => return cache value            |
| Each cache: cache value freshness > request freshness          | => try next cache               | => return cache value            |
| No value in any cache                                          | => try origin                   | => throw DataNotAvailableOffline |
| Origin value returned                                          | => return origin value          | => throw DataNotAvailableOffline |
| Origin connection exception, cache value freshness <= fallback | => return cache value           |                                  |
| Origin connection exception, cache value freshness > fallback  | => rethrow connection exception |                                  |


> At the time of writing, freshnessSeconds = RequestOp.FRESHNESS_INSIST (-1) was used to insist that the response data must either come immediately from the origin,
> or a DataNotAvailableOffline should be thrown.
> This value is now deprecated, to be replaced by passing freshnessSeconds = RequestOp.FRESHNESS_FRESHEST (0) and fallbackFreshnessSeconds = RequestOp.FALLBACK_NEVER.
> Freshness values should now be >= 0
>
> OLD: Cascade.Get<Model>(id, freshnessSeconds: RequestOp.FRESHNESS_INSIST)
> NEW: Cascade.Get<Model>(id, freshnessSeconds: RequestOp.FRESHNESS_FRESHEST, fallbackFreshnessSeconds: RequestOp.FALLBACK_NEVER)

## Optimal Caching for a sequence of Cascade requests (sequenceBeganMs)

When a sequence of related Cascade requests are performed, for optimum caching, the sequenceBeganMs parameter should be used to pass a common value captured from NowMs. 

Suppose we have configured cascade with a ModelClassCache layer and a API server origin. There are two models: Diary and DiaryItem. A Diary has many DiaryItem's, 
and a DiaryItem belongs to an Area.

We could write a static function like this :

```csharp
public static Task<Diary> GetDiary(CascadeDataLayer cascade, int diaryId, int freshnessSeconds) {
    var diary = cascade.Get<Diary>(id, freshnessSeconds: freshnessSeconds, populate: new [] {nameof(Diary.DiaryItems)});
    await cascade.Populate(diary.DiaryItems,nameof(DiaryItem.Diary),freshnessSeconds: freshnessSeconds);
    return diary;
}
```

Let's assume there are 3 DiaryItems associated with the Diary.

This would work perfectly as expected if we called it like this :

`var diary = await GetDiary(cascade,diaryId,3600);       // 1 hour freshness`

however if we called it like this :

`var diary = await GetDiary(cascade,diaryId,RequestOp.FRESHNESS_FRESHEST);   // 0 freshness`

and not cause any real problems, except for the following possible surprises :

1. the Diary property of each DiaryItem will be populated with a different Diary instance having the same id as the diary variable
2. none of the Diary instances will be the same as the diary variable instance
3. there will be 4 requests to the origin for the same Diary id

The reason is that for each request, the value of 0 (absolute freshness) will apply at the moment that each request is performed, which is different from the times of the other requests by mere milliseconds, but that is enough for each resulting Diary in the cache to be not considered fresh enough, and so another request to the origin is initiated. 
Conversely, when freshnessSeconds == 3600 the Get<Diary> request will be served by the origin and then the result instance is cached and returned 3 times for the Populate call.

The solution to this is the sequenceBeganMs parameter on all read request methods. We use it like this :

```csharp
public static Task<Diary> GetDiary(CascadeDataLayer cascade, int diaryId, int freshnessSeconds) {
    var sequenceBeganMs = cascade.NowMs;
    var diary = cascade.Get<Diary>(id,freshnessSeconds: freshnessSeconds, sequenceBeganMs: sequenceBeganMs, populate: new [] {nameof(Diary.DiaryItems)});
    await cascade.Populate(diary.DiaryItems,nameof(DiaryItem.Diary),freshnessSeconds: freshnessSeconds, sequenceBeganMs: sequenceBeganMs);
    return diary;
}
```

Now the same provided sequenceBeganMs value is used for all internal freshness calculations. All cache records that arrive after sequenceBeganMs now satisfy freshnessSeconds = RequestOp.FRESHNESS_FRESHEST no matter how many requests are performed with the sequenceBeganMs value. Please note that its probably not a good idea for the sequenceBeganMs value to be stored in a global or to be more than a few minutes different from real time (except for unit tests).  

Internally, CascadeDataLayer captures NowMs and passes it into all requests, and so using the populate option instead of the Populate method can save using the sequenceBeganMs parameter.

> Teaser :
> One day it might be possible to replace that whole method with the following line, but as of this writing the populate option only supports properties on the main object.
> 
> ```csharp
> var diary = cascade.Get<Diary>(id,freshnessSeconds: freshnessSeconds, populate: new [] {nameof(Diary.DiaryItems),nameof(Diary.DiaryItems)+"."+nameof(DiaryItem.Diary)});
> ```
