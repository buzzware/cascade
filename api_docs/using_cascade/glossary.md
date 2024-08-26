@page glossary Glossary

# Glossary

### Freshness

How recently did a data record arrive from the server? The value is represented in milliseconds and is inverse ie a value 
of 0 means the most fresh.

### Fallback Freshness

A value of acceptable freshness applied to the situation where ConnectionOnline == True and a request to the origin has failed due 
to network or server failure. In that situation, if the required value is in cache and is fresher (more recent than) than the 
fallback freshness value, it will be returned otherwise ignored.
