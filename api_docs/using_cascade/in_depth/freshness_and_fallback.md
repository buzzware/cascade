# Freshness and Fallback

The combination of freshness and fallback freshness enables applications to specify their requirements
in both typical online, online with network failure, and offline scenarios.

## Freshness

A key concept of Cascade is that applications specify the freshness of the data they require when they make Get, Query or BlobGet requests.
Cascade caches know the time of arrival of each record from the origin. 
Then it can be determined whether a record in cache is acceptable or not to meet the freshness requirement of the application request.

1) When ConnectionOnline == True, the freshness parameter is used to determine whether a value in cache is sufficiently fresh to be returned, or whether to 
re-request the data from the origin.
2) When ConnectionOnline == True and freshness < 0, the data is requested from the origin
3) When ConnectionOnline == False and freshness >= 0, any existing value in cache is returned regardless of how fresh it is.
4) When ConnectionOnline == False and freshness < 0, a DataNotAvailableOffline exception is thrown because freshness < 0 means that the application is 
insisting on absolutely fresh data from the server which is not available offline. 

## Fallback Freshness

When ConnectionOnline == True and a request to the origin has failed due to network or server failure,
a "Fallback Freshness" value determines whether a value in cache is acceptably fresh.
This achieves resilience in situations where the application is in online mode but the request fails due to abnormal network failure,
or being out of range.
