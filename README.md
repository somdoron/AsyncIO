AsyncIO
========

AsyncIO is portable high performance socket library for .Net. The library is based on Windows IO Completion ports.

.Net Socket library doesn't give control over the threads and doesn't expose the IO completion port API, AsyncIO give full control over the threads and allow the developer to create high performance servers.

On Mono the library fall down to mono implementation but still give completion port like API. 


