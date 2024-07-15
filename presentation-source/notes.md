

%%{init: {"theme": "dark"}}%%
flowchart TD
  View -- Commands --> ViewModels
  ViewModels --> View
  ViewModels --Get/Update etc--> Cascade
  Cascade --Models--> ViewModels
  Cascade <--> Caches["Caches (ICascadeCache)"]
  Cascade <--> Origin["Origin (ICascadeOrigin)"]
  Origin <--> Server


<img src="/images/cascade-one-way.png" style="height: 80vh"/>


          <div class="mermaid">
            <pre>
              stateDiagram-v2
                direction LR
                ONLINE --> TakeOffline
                state {`"Take Offline\neg. Query<Model>(\npopulate: associations,\nhold: true,\nfreshness: 0\n)"`} as TakeOffline
                TakeOffline --> OFFLINE: Disconnect Button\nConnectionOnline => False
                state "OFFLINE\n* Simulate Changes\n* Queue Changes Pending\n* Fallback Freshness" as OFFLINE
                OFFLINE --> ReconnectOnline: Reconnect Button
                ReconnectOnline --> ONLINE
                state ReconnectOnline {`{`}
                  state "UploadChangesPending()" as UploadChangesPending
                  state "ClearCache()" as ClearCache
                  UploadChangesPending --> ClearCache
                  state "RedownloadHeldRecords()" as RedownloadHeldRecords
                  ClearCache --> RedownloadHeldRecords
                  state "ConnectionOnline => True" as ConnectionOnlineTrue
                  RedownloadHeldRecords --> ConnectionOnlineTrue
                {`}`}
            </pre>
          </div>

