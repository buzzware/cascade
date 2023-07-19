
Through experimentation, it became evident that the server possesses a vast amount of data compared to what the front
end typically requires. The server incorporates its own rules and business logic governing data access. By caching the
results obtained from server queries, Cascade leverages the advanced caching capabilities of SQL. This eliminates the
need for frequent query executions, reducing resource consumption and enhancing overall efficiency.




At its core, Cascade stores data in files, utilizing a key-value store approach. File names serve as keys, while the
contents of the files represent the corresponding values. This file-based storage system simplifies data retrieval and
storage, contributing to the overall performance of the application.

The motivation behind developing Cascade stemmed from the challenges encountered when building mobile applications with
backends, models, and data requests. Managing data flow in such scenarios can become complicated and convoluted.
However, the concept of unidirectional data flow, introduced at the Facebook conference in 2008, provided a breakthrough
solution. This approach emphasizes the importance of establishing strict rules and structures that work harmoniously for
the majority of applications, while abstracting complexities away from developers.

Offline functionality was a critical aspect considered during the development of Cascade. Initially, the approach
focused on immutable models, ensuring that each model in Cascade remained consistent with its server representation.
However, modifications needed to be stored separately, capturing only the relevant changes. This allowed the separation
of server-originating data from app modifications. When data traversed the server, the merge of these two branches
occurred. However, the offline scenario necessitated keeping these branches separate and merging them as required.

To address offline operations, Cascade adopted a novel approach. Offline operations are executed locally, simulating
their effect on the server. These operations are returned alongside the results, enabling efficient execution and
subsequent data retrieval. Different types of operations, such as create and update, require specific handling methods
within Cascade. For instance, when updating, the framework applies the provided field updates to the existing values,
subsequently caching the modified result.

Offline support introduces specific requirements for the server's schema. Ideally, the server should support the usage
of unique IDs. Alternatively, negative integer IDs can be used by offline clients to prevent collisions with
server-generated IDs. Furthermore, it is advisable to utilize at least 64-bit IDs to accommodate multiple clients
generating IDs within different regions of the available space, eliminating the possibility of collisions.

Cascade emerges as a transformative framework, streamlining data flow within applications. By offering a robust data
layer, seamless caching, and efficient handling of offline scenarios, Cascade empowers developers to create consistent
and high-performing applications. Its unidirectional data flow ensures a structured and controlled environment for data
manipulation, allowing developers to focus on delivering exceptional user experiences.
