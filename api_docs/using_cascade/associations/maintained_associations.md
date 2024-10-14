# Associations Maintained through Create, Update and Replace Operations

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

1. HasMany and HasOne: association property values are simply copied from incoming model 
properties to the matching outgoing properties. If they are null on the incoming, 
they will be null on the outgoing.
2. BelongsTo and FromBlob: In general, association property values are copied from incoming model
properties to the matching outgoing properties. If they are null on the incoming,
   they will be null on the outgoing.<br/>
However in the special case that the association property value is no longer correct because their named 
idProperty/imagePath property has changed, then Populate will be used to set the
association correctly. This most commonly occurs when Update modifies the named idProperty.   

## Create Examples

```csharp
   department = new Department { id = 1, name = "HR" };
   
   employee = new Employee {
      departmentId = department.id,
      Department = department
   };
   createdEmployee = await cascade.Create(employee);
   // createdEmployee.Department == department  // maintained
   // createdEmployee.Photo == null
   
   employee = new Employee {
      departmentId = department.id,
   };
   createdEmployee = await cascade.Create(employee);
   // createdEmployee.departmentId == 1
   // createdEmployee.Department == null    // not automatically populated

   employee = new Employee {
      Department = department
   };
   createdEmployee = await cascade.Create(employee);
   // createdEmployee.Department == null  // not maintained because departmentId != Department.id 
```

## Update Examples

```csharp
   var department1 = new Department { id = 1, name = "HR" };
   var department2 = new Department { id = 2, name = "Science" };
   
   var employee = await cascade.Create(
      new Employee {
         departmentId = department.id,
         Department = department
      }
   );
   
   updated = await cascade.Update(employee, new Dictionary<string, object?> {
     { "departmentId", 2 }
   });
   // updated.departmentId == 2
   // updated.Department == null    // not automatically populated
   
   updated = await cascade.Update(employee, new Dictionary<string, object?> {
     { "departmentId", 2 },
     { "Department", department2 }
   });
   // updated.departmentId == 2
   // updated.Department == department2    // maintained
   
   updated = await cascade.Update(employee, new Dictionary<string, object?> {
     { "departmentId", 2 },
     { "Department", department1 }
   });
   // updated.departmentId == 2
   // updated.Department == department2    // corrected to match departmentId 
   
   updated = await cascade.Update(employee, new Dictionary<string, object?> {
     { "Department", department2 }
   });
   // updated.Department == null    // not maintained because departmentId == null
   
   employees = new Employee[] {
     new Employee { id = 1, name = "Fred" },
     new Employee { id = 2, name = "Sally" }
   };
   
   updateDepartment = await cascade.Update(department1, new Dictionary<string, object?> {
     { "name", "Jane" },
     { "Employees", employees },
   });
   // updateDepartment.name == "Jane"
   // updateDepartment.Employees == employees
   
```
