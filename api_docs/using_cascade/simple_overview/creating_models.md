@page creating_models Creating Models 

## Instantiating Model Instances in Application Memory

Cascade provides a straightforward method to create new models in your Origin. Cascade model classes, which inherit from SuperModel, can be used like regular C# classes while providing additional functionality for data binding and change tracking. Here's how you can work with them:

1. Creating a New Instance:
   You can create a new instance of a model class using its constructor:

   ```csharp
   var newDocket = new Docket() {
        description = "New shipment"
   };
   ```

2. Setting Properties:
   Set properties as you would with any C# object:

   ```csharp
   newDocket.description = "New shipment";
   newDocket.docketDate = DateTime.Now;
   newDocket.quantity = 5;
   ```

3. Property Change Notifications:
   The SuperModel base class implements INotifyPropertyChanged. When you set a property, it automatically raises PropertyChanged events, which UI frameworks can listen to for updating the UI:

   ```csharp
   newDocket.PropertyChanged += (sender, args) => {
       Console.WriteLine($"Property {args.PropertyName} changed");
   };
   ```

4. Data Binding:
   Due to the implementation of INotifyPropertyChanged, you can easily bind these properties to UI elements in frameworks like WPF, Xamarin.Forms, or MAUI:

   ```xaml
   <Entry Text="{Binding description}" />
   <DatePicker Date="{Binding docketDate}" />
   <Entry Text="{Binding quantity}" />
   ```

5. Form Building:
   When building forms, you can create a new instance of the model and bind it to the form. As the user interacts with the form, the model properties are modified and any bound controls are updated automatically:

   ```csharp
   public class DocketFormViewModel
   {
       public Docket NewDocket { get; } = new Docket();
   }
   ```

6. Validation:
   You can implement validation logic within the setters of your properties or in a separate method. The UI can then bind to these validation results.

7. Preparing for Creation:
   Once all necessary properties are set and validated, you can pass the model instance to Cascade's Create method:

   ```csharp
   public async Task SaveDocket()
   {
       if (IsValid(NewDocket))
       {
           var createdDocket = await AppCommon.Cascade.Create(NewDocket);
           // Handle successful creation
       }
   }
   ```

This approach allows you to work with your model classes in a familiar object-oriented manner while leveraging Cascade's features for data persistence and UI integration. The separation between model instantiation/modification and the actual creation on the server provides flexibility in how you structure your application logic and user interactions.


## Creating Models in Cascade Caches and Origin

### The Create Method

The primary method for creating a new model is the `Create` method:

```csharp
public async Task<M> Create<M>(M model) where M : class
```

#### Parameters

- `M`: The type of model you're creating.
- `model`: An instance of the model with the properties you want to set.

#### Return Value

The method returns a `Task<M>`, where `M` is the type of model you're creating. The returned model typically includes any server-generated fields, such as the ID.


### Handling IDs

Setting the ID is optional when creating a new model, depending on the Origin. Servers will typically generates the id automatically.

### Basic Usage

Here's a simple example of how to use the `Create` method to create a new Docket:

```csharp
public async Task<Docket> CreateDocket(Docket newDocket)
{
    Docket createdDocket = await AppCommon.Cascade.Create(newDocket);
    return createdDocket;
}
```
