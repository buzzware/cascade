# Associations

> “The programmer, like the poet, works only slightly removed from pure thought-stuff. He builds his castles in the air, from air, creating by exertion of the imagination. Few media of creation are so flexible, so easy to polish and rework, so readily capable of realizing grand conceptual structures.”
― Frederick P. Brooks, "The Mythical Man Month", 1975

Cascade assists the developer in creating and maintaining data structures in application memory that typically consist of a view model, models, collections of models and possibly other types with references between them.

Cascade defines methods and attributes for maintaining model or IEnumerable<model> reference properties, known as "associations".

In database Entity Relationship Diagrams, relationships between records are normally one-to-one, one-to-many or many-to-one. These three
kinds of relationships are all implemented by a foreign key id referencing the id of another table. A one-to-many is the other side of a many-to-one. 
A fourth kind of relationship - many-to-many is really an intermediate table with two many-to-one relationships.
Therefore, all of these kinds of relationships are implemented by :

1. foreign keys referencing other models by their primary key and type, and the ability to retrieve the referenced model
2. the ability to retrieve all models of a foreign type that reference a local model by its id and type

Cascade provides the following attributes for declaring associations on Cascade models. The name associations and the naming of each association type comes from [Ruby On Rails](https://edgeguides.rubyonrails.org/association_basics.html)

## BelongsTo

A DocketItem "belongs to" a single Docket, and the DocketItem has :

1. a foreign key property of the same type as the Docket id to reference the Docket.
2. a foreign association reference property of the Docket Type, and the BelongsTo attribute specifying the idProperty as the foreign key property by name.

```csharp
	public class DocketItem : SuperModel {
			
		[CascadeId]
		public string id {
			get => GetProperty(ref _id); 
			set => SetProperty(ref _id, value);
		}
		private string _id;
		
		public string docketId {
			get => GetProperty(ref _docketId); 
			set => SetProperty(ref _docketId, value);
		}
		private string _docketId;

		[BelongsTo(idProperty: nameof(docketId))]
		public Docket Docket {
			get => GetProperty(ref _Docket); 
			set => SetProperty(ref _Docket, value);
		}
		private Docket _Docket;
    }
```

## HasMany

A Docket "has many" DocketItems, and the Docket has:

1. a collection property of ImmutableArray<DocketItem> type to hold the associated DocketItems.
2. a HasMany attribute on the collection property specifying the foreignIdProperty as the foreign key property on the DocketItem by name.

```csharp
public class Docket : SuperModel
{
    [CascadeId]
    public string id
    {
        get => GetProperty(ref _id);
        set => SetProperty(ref _id, value);
    }
    private string _id;

    [HasMany(foreignIdProperty: nameof(DocketItem.docketId))]
    public ImmutableArray<DocketItem> Items
    {
        get => GetProperty(ref _Items);
        set => SetProperty(ref _Items, value);
    }
    private ImmutableArray<DocketItem> _Items;
}
```

In this example, the `Docket` class has a collection of `DocketItem` objects. The `HasMany` attribute is applied to the `Items` property, indicating that a `Docket` can have multiple `DocketItem` instances associated with it. The `foreignIdProperty` parameter in the `HasMany` attribute specifies that the `docketId` property in the `DocketItem` class is used as the foreign key to establish this relationship.

The `ImmutableArray<DocketItem>` type is used for the collection to ensure thread-safety and to prevent accidental modifications of the collection itself. The actual management of this collection (adding, removing items) is handled by Cascade based on the relationship defined by the `HasMany` attribute.

## HasOne

A DocketItem "has one" DocketReceiptItem, and the DocketItem has:

1. a reference property of DocketReceiptItem type to hold the associated DocketReceiptItem.
2. a HasOne attribute on the reference property specifying the foreignIdProperty as the foreign key property on the DocketReceiptItem by name.

```csharp
public class DocketItem : SuperModel
{
    [CascadeId]
    public string id
    {
        get => GetProperty(ref _id);
        set => SetProperty(ref _id, value);
    }
    private string _id;

    [HasOne(foreignIdProperty: nameof(DocketReceiptItem.docketItemId))]
    public DocketReceiptItem DocketReceiptItem
    {
        get => GetProperty(ref _DocketReceiptItem);
        set => SetProperty(ref _DocketReceiptItem, value);
    }
    private DocketReceiptItem _DocketReceiptItem;
}
```

In this example, the `DocketItem` class has a reference to a single `DocketReceiptItem` object. The `HasOne` attribute is applied to the `DocketReceiptItem` property, indicating that a `DocketItem` can have one associated `DocketReceiptItem` instance. The `foreignIdProperty` parameter in the `HasOne` attribute specifies that the `docketItemId` property in the `DocketReceiptItem` class is used as the foreign key to establish this relationship.

## FromBlob

A ThingPhoto has properties : 

1. imagePath - a normal string data property which is a relative storage path as used by BlobGet/BlobPut.
2. Image - an association property with the FromBlob attribute referencing imagePath and specifying DotNetBitmapConverter.

Populating the Image property will internally call BlobGet with imagePath, then the result will be converted to a Bitmap by 
DotNetBitmapConverter and used to set Image.

```csharp
public class ThingPhoto : SuperModel
{

    public string? imagePath {
      get => GetProperty(ref _ImagePath); 
      set => SetProperty(ref _ImagePath, value);
    }
    private string? _ImagePath;
    
    /// <summary>
    /// Bitmap of the image
    /// </summary>
    [FromBlob(nameof(imagePath),typeof(DotNetBitmapConverter))]
    public Bitmap? Image {
      get => GetProperty(ref _Image); 
      set => SetProperty(ref _Image, value);
    }
    private Bitmap? _Image;
}
```
