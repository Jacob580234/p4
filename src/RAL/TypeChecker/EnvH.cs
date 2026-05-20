namespace RAL.TC;
using RAL.AST;


public class EnvH {

    //Map: Each category to its immediate supertype. If none supplied at declaration, Root "Resource" is immediate super.
    private readonly Dictionary<ResourceT, ResourceT?> parentCatRelations = new() { 
        {new ResourceT("Resource"), null} 
    }; 

    /// <summary> Boolean lookup function "envH()": True if first argument is of same type or descendant of second </summary>
    public bool IsSubtype(ResourceT childCat, ResourceT parentCat) {

        while (childCat != null) {
            if (childCat == parentCat) 
                return true;
            
            if (!parentCatRelations.TryGetValue(childCat, out childCat)) 
                break;
        }
        return false;
    }



    public bool EstablishRelation(ResourceT childCat, ResourceT parentCat) {
        // needs to be exception, otherwise lookups may enter infinite loop
        if(childCat == parentCat) 
            throw new Exception("A category may not relate to itself"); 
        if(parentCatRelations.ContainsKey(childCat)) return false;
            parentCatRelations.Add(childCat, parentCat);
        return true;
    } 

/// <summary> Under valid input, guarentees parentCat. Nullable due to 'Resource' having no parentCat. Otherwise, throws exception <summary>
    public bool TryGetParent(ResourceT childCat, out ResourceT? parent) {
        return parentCatRelations.TryGetValue(childCat, out parent);
    }

     public IEnumerable<ResourceT>? GetAllRelated(ResourceT category) {
        //Set of all related nodes, ancesters and decendants
        var result = new HashSet<ResourceT>();

        //Upwards, ancesters
        ResourceT? current = category;
        while (current != null) {
            result.Add(current);
            if (!TryGetParent(current, out ResourceT? parent)) {
                //Indicates invalid input
                return null;
            }

            current = parent;
        }
        
        // Downwards, descendants
        foreach (ResourceT node in GetSubTree(category)) {
            result.Add(node);
        }

        return result;
    }

    /// <summary> flat enumarable of subCategories (including itself), organised by descendant level </summary>
    /// Used by: LookupCategoryPropertyType (reserve where-clause only)
    public IEnumerable<ResourceT> GetSubTree(ResourceT category) {
        
        HashSet<ResourceT> subCategories  = [];
        
        //breadth first, i.e. organised by nearest descendants
        var queue = new Queue<ResourceT>();
        queue.Enqueue(category);

        while (queue.Count > 0) {
            ResourceT current = queue.Dequeue();
            subCategories.Add(current);

            foreach (ResourceT child in GetChildren(current))
                queue.Enqueue(child);
        }

        return subCategories;
    }

    /// <summary> Find all categories whose immediate parent is 'current' </summary>
    private IEnumerable<ResourceT> GetChildren(ResourceT category) =>  
        parentCatRelations
            .Where(relation => relation.Value == category)
            .Select(relation => relation.Key);

}