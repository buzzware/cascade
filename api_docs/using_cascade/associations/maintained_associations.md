@page maintained_associations Associations Maintained through Create, Update and Replace Operations

The Cascade methods Create, Update and Replace receive a SuperModel that may have 
association properties already set. For example, because you previously populated them 
using the Populate method or option on Get or Query.

Previously, because Cascade uses the immutable model approach where changes 
return a new instance, those association properties were null on the returned instance.
Now, however, their values are "maintained" from input to output. This makes applications 
more intuitive and less laborious (no need to re-Populate associations you populated in 
the original Get/Query).
In any case, models passed in are not modified, and a new instance is always returned. 

## Rules for Maintaining Associations

For each association property, the "incoming" value considered is the value from the changes dictionary
(for Update, when the association property name occurs in the changes), otherwise the value of the property
on the model instance passed in. Incoming values that are null are ignored, leaving the returned instance's
property null.

1. HasMany and HasOne: incoming association property values are simply copied to the matching property
on the returned instance. If they are null on the incoming, they will be null on the outgoing.
2. BelongsTo and FromBlob: these associations are derived from a named key property (idProperty for BelongsTo,
pathProperty for FromBlob). When the key property value is unchanged between the input model and the result,
the incoming association value is maintained. However in the special case that the association value would no
longer be correct because the named idProperty/pathProperty value has changed (most commonly when Update
modifies the named idProperty), Cascade instead re-populates the association property on the returned instance
(using freshness ANY ie. from cache when possible) so that it matches the new key value.

> Note: it is the application's responsibility to keep the foreign key property and the association property
> consistent when setting both, eg. `employee.departmentId` should equal `employee.Department.id`.

## Create Examples

```csharp
   department = await cascade.Create(new Department { id = 1, name = "HR" });
   
   employee = await cascade.Create(new Employee {
      departmentId = department.id,
      Department = department
   });
   // employee.departmentId == 1
   // employee.Department.id == 1  // maintained
   // employee.Photo == null       // was null incoming, so null outgoing
   
   employee = await cascade.Create(new Employee {
      departmentId = department.id,
   });
   // employee.departmentId == 1
   // employee.Department == null  // not automatically populated
```

## Update Examples

```csharp
   var department1 = await cascade.Create(new Department { id = 1, name = "HR" });
   var department2 = await cascade.Create(new Department { id = 2, name = "Science" });
   
   var employee = await cascade.Create(
      new Employee {
         departmentId = department1.id,
         Department = department1
      }
   );
   
   // change that doesn't touch the association or its key :
   updated = await cascade.Update(employee, new Dictionary<string, object?> {
     { "name", "Fred" }
   });
   // updated.name == "Fred"
   // updated.Department.id == 1   // maintained
   
   // change the foreign key :
   updated = await cascade.Update(employee, new Dictionary<string, object?> {
     { "departmentId", 2 }
   });
   // updated.departmentId == 2
   // updated.Department.id == 2   // re-populated to match the new departmentId
   
   // change the foreign key and provide the association value :
   updated = await cascade.Update(employee, new Dictionary<string, object?> {
     { "departmentId", 2 },
     { "Department", department2 }
   });
   // updated.departmentId == 2
   // updated.Department.id == 2   // re-populated because departmentId changed
   
   // HasMany values in changes are simply copied :
   employees = new Employee[] {
     new Employee { id = 1, name = "Fred" },
     new Employee { id = 2, name = "Sally" }
   };
   
   updatedDepartment = await cascade.Update(department1, new Dictionary<string, object?> {
     { "name", "Engineering" },
     { "Employees", employees },
   });
   // updatedDepartment.name == "Engineering"
   // updatedDepartment.Employees == employees   // maintained
```

See MaintainsAssociationsTests.cs for working examples of this behaviour.
