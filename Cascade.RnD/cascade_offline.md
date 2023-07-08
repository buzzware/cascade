# Cascade Offline Notes


## Original ProcessRequest

```C#
GetResponse
    ProcessRequest
        innerProcess
            ProcessReadOrQuery|ProcessGetCollection|ProcessCreate   // 
                layer.Fetch
                Origin.ProcessRequest
                    CivtracSimpleClassOrigin.Get|Create|Replace     // simple http & (de)serialization
                Populate
                    processHasMany|processHasOne|processBelongsTo
                        ProcessRequest
                        SetModelCollectionProperty        
                StoreInPreviousCaches
```

## Proposed ProcessRequest

Rearranging method heirarchy to avoid infinite recursion, allow self contained sub-operations to be called within original operation (Get|Query etc)  

```C#

MaybeProcessPendingChanges
    if (HavePendingChanges && shouldAttemptUploadPendingChanges) {
        try {        
            if (Cascade.ConnectionMode!=UploadingPendingChanges)
                 Cascade.ConnectionMode = UploadingPendingChanges;
            UploadPendingChanges
                InnerProcess(mode=online)            // !!! should only attempt online processing
            Cascade.ConnectionMode = Online
        } catch (OfflineException) {
            // probably smother exceptions. If we can't complete UploadPendingChanges(), stay offline
        }
    }

GetResponse
    ProcessRequest                                                  // public method that can be used by app code to process a RequestOp                   
        MaybeProcessPendingChanges
        InnerProcessWithOfflineFallback                      
            bool loop;
            do {                
                try {
                    loop = false
                    InnerProcess(mode=Cascade.ConnectionMode==Online ? online:offline)
                        ProcessReadOrQuery|ProcessGetCollection|ProcessCreate
                            layer.Fetch
                            Origin.ProcessRequest
                                CivtracSimpleClassOrigin.Get|Create|Replace
                                    HTTP GET|POST etc                    
                } catch (OfflineException) {
                    switch(Cascade.ConnectionMode) {
                        case Online:
                            Cascade.ConnectionMode = Offline;
                            if (offline)
                                loop = true;                    
                        case Offline:
                            Log.Warning("Should not get OfflineException when ConnectionMode != Online");
                }
            } while (loop);
        StoreInPreviousCaches
        Populate
            processHasMany|processHasOne|processBelongsTo
                InnerProcessWithOfflineFallback
                StoreInPreviousCaches
                SetModelCollectionProperty
```

## How to merge in pending changes offline ?

* InnerProcess can loop through PendingChanges looking for relevent update, replace, delete
* "relevant" means match of Type & Id
* How to handle execute ?

## Before implementing

1. Refactor to move SetResultsImmutable & StoreInPreviousCaches out
2. 
