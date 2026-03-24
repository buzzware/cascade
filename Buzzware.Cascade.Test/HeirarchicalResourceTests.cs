using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using Buzzware.StandardExceptions;
using NUnit.Framework;
using Serilog;

namespace Buzzware.Cascade.Test {

  /// <summary>
  /// A custom IModelClassOrigin for Child2 that simulates hierarchical REST URLs
  /// like /parent/:parent_id/child/:child_id backed by a Dictionary.
  /// </summary>
  public class HierarchicalChild2Origin : IModelClassOrigin {

    public ICascadeOrigin Origin { get; set; }

    /// <summary>
    /// The backing store keyed by full URL paths eg "/parent/abc/child/xyz"
    /// </summary>
    public readonly Dictionary<string, Child2> Store = new Dictionary<string, Child2>();

    private string UrlForChild(Child2 child) {
      return $"/parent/{child.parentId}/child/{child.id}";
    }

    private string UrlForChild(string parentId, string childId) {
      return $"/parent/{parentId}/child/{childId}";
    }

    public async Task<IEnumerable> Query(object criteria, RequestOp requestOp) {
      // criteria should contain parentId for filtering
      JsonElement? crit = criteria as JsonElement?;
      if (crit == null)
        crit = JsonSerializer.SerializeToElement(criteria);

      var parentId = crit.Value.EnumerateObject()
        .FirstOrDefault(p => p.Name == "parentId");

      if (parentId.Value.ValueKind != JsonValueKind.Undefined) {
        var pid = parentId.Value.GetString();
        return Store
          .Where(kv => kv.Key.StartsWith($"/parent/{pid!}/child/"))
          .Select(kv => kv.Value)
          .ToImmutableArray();
      }

      return Store.Values.ToImmutableArray();
    }

    public async Task<object?> Get(object id, RequestOp requestOp) {
      var cascadeKey = id?.ToString();
      if (cascadeKey == null)
        return null;

      // CascadeKey is "parentId__childId", convert to URL
      var parts = cascadeKey.Split(new[] { "__" }, 2, StringSplitOptions.None);
      if (parts.Length == 2) {
        var url = UrlForChild(parts[0], parts[1]);
        Store.TryGetValue(url, out var result);
        return result;
      }

      return null;
    }

    public async Task<object> Create(object value, RequestOp requestOp) {
      var child = (Child2)value;
      var id = child.id ?? Origin.NewGuid();
      var created = new Child2 { id = id, parentId = child.parentId, name = child.name };
      var url = UrlForChild(created);
      Store[url] = created;
      return created;
    }

    public async Task<object> Replace(object value, RequestOp requestOp) {
      var child = (Child2)value;
      var url = UrlForChild(child);
      Store[url] = child;
      return child;
    }

    public async Task<object> Update(object id, IDictionary<string, object?> changes, object? model, RequestOp requestOp) {
      var child = (Child2)model!;
      var url = UrlForChild(child);
      if (!Store.TryGetValue(url, out var existing))
        throw new Exception($"Child2 not found at {url}");

      var updated = new Child2 { id = existing.id, parentId = existing.parentId, name = existing.name };
      updated.__ApplyChanges(changes);
      Store[url] = updated;
      return updated;
    }

    public async Task Destroy(object model, RequestOp requestOp) {
      var child = (Child2)model;
      var url = UrlForChild(child);
      Store.Remove(url);
    }

    public async Task EnsureAuthenticated() { }
    public async Task ClearAuthentication() { }

    public Task<object?> Execute(RequestOp request, bool connectionOnline) {
      throw new NotImplementedException();
    }
  }

  /// <summary>
  /// Tests for hierarchical resource URLs like /parent/:parent_id/child/:child_id
  /// </summary>
  [TestFixture]
  public class HeirarchicalResourceTests {

    private string testSourcePath;
    private string tempDir;
    private string testClassName;
    private string testName;

    MockOrigin2 origin;
    MockModelClassOrigin<Parent2> parentOrigin;
    HierarchicalChild2Origin childOrigin;
    MockModelClassOrigin<Toy> toyOrigin;
    CascadeDataLayer cascade;
    private ModelClassCache<Parent2, string> parentMemoryCache;
    private ModelClassCache<Child2, string> childMemoryCache;
    private ModelClassCache<Toy, string> toyMemoryCache;
    private ModelCache memoryCache;
    private FastFileClassCache<Parent2, string> parentFileCache;
    private FastFileClassCache<Child2, string> childFileCache;
    private FastFileClassCache<Toy, string> toyFileCache;
    private ModelCache fileCache;

    [SetUp]
    public void SetUp() {
      testClassName = TestContext.CurrentContext.Test.ClassName.Split('.').Last();
      testName = TestContext.CurrentContext.Test.Name;
      testSourcePath = CascadeUtils.AboveFolderNamed(TestContext.CurrentContext.TestDirectory, "bin")!;
      tempDir = testSourcePath + $"/temp/{testClassName}.{testName}";

      Log.Debug($"Test tempDir {tempDir}");

      if (Directory.Exists(tempDir))
        Directory.Delete(tempDir, true);
      Directory.CreateDirectory(tempDir);

      var cascadeDir = tempDir + "/Cascade";

      parentOrigin = new MockModelClassOrigin<Parent2>();
      childOrigin = new HierarchicalChild2Origin();
      toyOrigin = new MockModelClassOrigin<Toy>();

      origin = new MockOrigin2(
        new Dictionary<Type, IModelClassOrigin>() {
          { typeof(Parent2), parentOrigin },
          { typeof(Child2), childOrigin },
          { typeof(Toy), toyOrigin }
        },
        1000
      );

      parentMemoryCache = new ModelClassCache<Parent2, string>();
      childMemoryCache = new ModelClassCache<Child2, string>();
      toyMemoryCache = new ModelClassCache<Toy, string>();

      memoryCache = new ModelCache(
        aClassCache: new Dictionary<Type, IModelClassCache>() {
          { typeof(Parent2), parentMemoryCache },
          { typeof(Child2), childMemoryCache },
          { typeof(Toy), toyMemoryCache }
        }
      );

      parentFileCache = new FastFileClassCache<Parent2, string>(cascadeDir);
      childFileCache = new FastFileClassCache<Child2, string>(cascadeDir);
      toyFileCache = new FastFileClassCache<Toy, string>(cascadeDir);

      fileCache = new ModelCache(
        aClassCache: new Dictionary<Type, IModelClassCache>() {
          { typeof(Parent2), parentFileCache },
          { typeof(Child2), childFileCache },
          { typeof(Toy), toyFileCache }
        }
      );

      cascade = new CascadeDataLayer(
        origin,
        new ICascadeCache[] { memoryCache, fileCache },
        new CascadeConfig() { StoragePath = cascadeDir },
        new MockCascadePlatform(),
        ErrorControl.Instance,
        new CascadeJsonSerialization()
      );
    }

    [TearDown]
    public void TearDown() {
      if (Directory.Exists(tempDir))
        Directory.Delete(tempDir, true);
    }

    private void SeedData() {
      // Seed parents into parentOrigin
      var p1 = new Parent2 { id = "p1", name = "Alpha" };
      var p2 = new Parent2 { id = "p2", name = "Beta" };
      parentOrigin.Store(p1.id, p1).Wait();
      parentOrigin.Store(p2.id, p2).Wait();

      // Seed children into hierarchical store
      var c1 = new Child2 { id = "c1", parentId = "p1", name = "Child One" };
      var c2 = new Child2 { id = "c2", parentId = "p1", name = "Child Two" };
      var c3 = new Child2 { id = "c3", parentId = "p2", name = "Child Three" };
      childOrigin.Store[$"/parent/p1/child/c1"] = c1;
      childOrigin.Store[$"/parent/p1/child/c2"] = c2;
      childOrigin.Store[$"/parent/p2/child/c3"] = c3;

      // Seed toys - childId uses Child2's compound CascadeKey
      var t1 = new Toy { id = "t1", childId = "p1__c1", name = "Ball" };
      var t2 = new Toy { id = "t2", childId = "p1__c1", name = "Doll" };
      var t3 = new Toy { id = "t3", childId = "p2__c3", name = "Car" };
      toyOrigin.Store("t1", t1).Wait();
      toyOrigin.Store("t2", t2).Wait();
      toyOrigin.Store("t3", t3).Wait();
    }

    [Test]
    public async Task QueryParents() {
      SeedData();

      var parents = await cascade.Query<Parent2>(
        "all_parents",
        new JsonObject(),
        freshnessSeconds: 0
      );

      Assert.That(parents.Count(), Is.EqualTo(2));
      Assert.That(parents.Any(p => p.id == "p1"), Is.True);
      Assert.That(parents.Any(p => p.id == "p2"), Is.True);
    }

    [Test]
    public async Task PopulateChildren() {
      SeedData();

      var parents = (await cascade.Query<Parent2>(
        "all_parents",
        new JsonObject(),
        freshnessSeconds: 0
      )).ToImmutableArray();

      // Populate Children association on all parents
      await cascade.Populate(parents, new[] { "Children" }, freshnessSeconds: 0);

      var p1 = parents.First(p => p.id == "p1");
      var p2 = parents.First(p => p.id == "p2");

      Assert.That(p1.Children, Is.Not.Null);
      Assert.That(p1.Children!.Count, Is.EqualTo(2));
      Assert.That(p1.Children.Any(c => c.id == "c1"), Is.True);
      Assert.That(p1.Children.Any(c => c.id == "c2"), Is.True);

      Assert.That(p2.Children, Is.Not.Null);
      Assert.That(p2.Children!.Count, Is.EqualTo(1));
      Assert.That(p2.Children.First().id, Is.EqualTo("c3"));
    }

    [Test]
    public async Task GetChildByCompoundId() {
      SeedData();

      // Child2's CascadeKey is "parentId__id"
      var child = await cascade.Get<Child2>("p1__c1", freshnessSeconds: 0);

      Assert.That(child, Is.Not.Null);
      Assert.That(child!.id, Is.EqualTo("c1"));
      Assert.That(child.parentId, Is.EqualTo("p1"));
      Assert.That(child.name, Is.EqualTo("Child One"));
    }

    [Test]
    public async Task UpdateChild() {
      SeedData();

      var child = await cascade.Get<Child2>("p1__c1", freshnessSeconds: 0);
      Assert.That(child, Is.Not.Null);

      var updated = await cascade.Update(child!, new Dictionary<string, object?> {
        { "name", "Fred" }
      });

      Assert.That(updated, Is.Not.Null);
      Assert.That(updated!.name, Is.EqualTo("Fred"));
      Assert.That(updated.id, Is.EqualTo("c1"));
      Assert.That(updated.parentId, Is.EqualTo("p1"));

      // Verify the origin store was updated
      Assert.That(childOrigin.Store["/parent/p1/child/c1"].name, Is.EqualTo("Fred"));
    }

    [Test]
    public async Task DestroyChild() {
      SeedData();

      var child = await cascade.Get<Child2>("p1__c1", freshnessSeconds: 0);
      Assert.That(child, Is.Not.Null);

      await cascade.Destroy(child!);

      // Verify removed from origin store
      Assert.That(childOrigin.Store.ContainsKey("/parent/p1/child/c1"), Is.False);

      // Other children should remain
      Assert.That(childOrigin.Store.ContainsKey("/parent/p1/child/c2"), Is.True);
      Assert.That(childOrigin.Store.ContainsKey("/parent/p2/child/c3"), Is.True);
    }

    [Test]
    public async Task QueryChildrenByParentUrl() {
      SeedData();

      // Query children for a specific parent - simulates GET /parent/p1/child/
      var children = await cascade.Query<Child2>(
        "p1_children",
        new JsonObject { ["parentId"] = "p1" },
        freshnessSeconds: 0
      );

      Assert.That(children.Count(), Is.EqualTo(2));
      Assert.That(children.All(c => c.parentId == "p1"), Is.True);
    }

    [Test]
    public async Task PopulateToysOnChild() {
      SeedData();

      var child = await cascade.Get<Child2>("p1__c1", freshnessSeconds: 0);
      Assert.That(child, Is.Not.Null);

      await cascade.Populate(child!, new[] { "Toys" }, freshnessSeconds: 0);

      Assert.That(child!.Toys, Is.Not.Null);
      Assert.That(child.Toys!.Count, Is.EqualTo(2));
      Assert.That(child.Toys.Any(t => t.id == "t1" && t.name == "Ball"), Is.True);
      Assert.That(child.Toys.Any(t => t.id == "t2" && t.name == "Doll"), Is.True);
    }

    [Test]
    public async Task PopulateToysOnChildWithNoToys() {
      SeedData();

      var child = await cascade.Get<Child2>("p1__c2", freshnessSeconds: 0);
      Assert.That(child, Is.Not.Null);

      await cascade.Populate(child!, new[] { "Toys" }, freshnessSeconds: 0);

      Assert.That(child!.Toys, Is.Not.Null);
      Assert.That(child.Toys!.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task PopulateChildOnToy() {
      SeedData();

      var toy = await cascade.Get<Toy>("t1", freshnessSeconds: 0);
      Assert.That(toy, Is.Not.Null);
      Assert.That(toy!.childId, Is.EqualTo("p1__c1"));

      await cascade.Populate(toy, new[] { "Child" }, freshnessSeconds: 0);

      Assert.That(toy.Child, Is.Not.Null);
      Assert.That(toy.Child.id, Is.EqualTo("c1"));
      Assert.That(toy.Child.parentId, Is.EqualTo("p1"));
      Assert.That(toy.Child.name, Is.EqualTo("Child One"));
    }

    [Test]
    public async Task PopulateFullHierarchy() {
      SeedData();

      // Get parent, populate children, then populate toys on each child
      var parent = await cascade.Get<Parent2>("p1", freshnessSeconds: 0);
      Assert.That(parent, Is.Not.Null);

      await cascade.Populate(parent!, new[] { "Children" }, freshnessSeconds: 0);
      Assert.That(parent!.Children, Is.Not.Null);
      Assert.That(parent.Children!.Count, Is.EqualTo(2));

      // Populate Toys on all children
      await cascade.Populate(parent.Children, "Toys", freshnessSeconds: 0);

      var c1 = parent.Children.First(c => c.id == "c1");
      var c2 = parent.Children.First(c => c.id == "c2");

      Assert.That(c1.Toys, Is.Not.Null);
      Assert.That(c1.Toys!.Count, Is.EqualTo(2));

      Assert.That(c2.Toys, Is.Not.Null);
      Assert.That(c2.Toys!.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task CreateChildAndEnsureInParentChildren() {
      SeedData();

      // Get parent and populate its Children
      var parent = await cascade.Get<Parent2>("p1", freshnessSeconds: 0);
      Assert.That(parent, Is.Not.Null);
      await cascade.Populate(parent!, new[] { "Children" }, freshnessSeconds: 0);
      Assert.That(parent!.Children!.Count, Is.EqualTo(2));

      // Create a new child via cascade - id should be assigned by the origin
      var newChild = await cascade.Create(new Child2 { parentId = "p1", name = "Child Four" });
      Assert.That(newChild, Is.Not.Null);
      Assert.That(newChild!.id, Is.Not.Null.And.Not.Empty);
      Assert.That(newChild.parentId, Is.EqualTo("p1"));
      Assert.That(newChild.name, Is.EqualTo("Child Four"));

      // Ensure the new child is in the parent's Children collection
      await cascade.HasManyEnsureItem(parent, nameof(Parent2.Children), newChild);

      Assert.That(parent.Children, Is.Not.Null);
      Assert.That(parent.Children!.Count, Is.EqualTo(3));
      Assert.That(parent.Children.Any(c => c.id == newChild.id && c.name == "Child Four"), Is.True);
      
      parent.__mutateWith(p => ((Parent2)p).Children = null);

    // Populate Children again with zero freshness to force re-fetch from origin
      await cascade.Populate(parent, new[] { "Children" }, freshnessSeconds: 0);

      Assert.That(parent.Children, Is.Not.Null);
      Assert.That(parent.Children!.Count, Is.EqualTo(3));
      Assert.That(parent.Children.Any(c => c.id == newChild.id && c.name == "Child Four"), Is.True);
      // Original children should still be there
      Assert.That(parent.Children.Any(c => c.id == "c1"), Is.True);
      Assert.That(parent.Children.Any(c => c.id == "c2"), Is.True);
    }
  }
}
