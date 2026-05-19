namespace RAL.Interpreter;

//CategoryId -> Resources of category
public class ResourceRegistry {


    //Singleton instance of registry. Static, i.e. a single class field for entire application 
    private static readonly ResourceRegistry instance = new ResourceRegistry();

    //Instance field on the singleton instance, therefor not static. Holds the registry data structure 
    private readonly Dictionary<string, HashSet<ResourceVal>> _registry;

    //Private constructor initializing registry data structure
    private ResourceRegistry() {
                                //categoryId
        _registry = new Dictionary<string, HashSet<ResourceVal>>() { { "Resource", new HashSet<ResourceVal>() } };
        
    }

    //Method to access singleton, which is otherwise private
    public static ResourceRegistry Instance() { return instance;  }

/// <summary> Moves a resource ot another category/// </summary>
    public void MoveResource(ResourceVal resource, string newCategoryId) {
        GetResourceList(resource.CategoryId).Remove(resource);
        AddResource(newCategoryId, resource);
    }

    /// <summary> Adds a resource to a category </summary>
    public void AddResource(string categoryId, ResourceVal resource) {

        GetResourceList(categoryId).Add(resource);
    }

    public IEnumerable<ResourceVal> GetAllResourcesInCategorySubtree(IEnumerable<string> subCategories)
        => subCategories.SelectMany(category => GetResourceList(category));


    private HashSet<ResourceVal> GetResourceList(string categoryId) {
        
        //All registered categories must have a list to return, wether empty or not. Exception wanted
        return _registry[categoryId];
    }

    /// <summary> Called upon category declaration. </summary>
    public void RegisterCategory(string categoryId)
    {
        // Type-checker should ensure no duplicate declarations, i.e. no duplicate keys. Exceptions are wanted
        _registry.Add(categoryId, new HashSet<ResourceVal>());
    }

     public override string ToString() {

        string s = "____________Printing Resource Registry:____________\n\n";

        foreach (var category in _registry) {
            s += $"__In {category.Key} category:__\n\n";

            foreach (ResourceVal resource in category.Value) {
                s += resource.ToString() + "\n";
            }

            s += $"\n__End of {category.Key} category__\n\n";;
        }

        s+= "____________Registry Printing Done____________";

        return s;
    }
}