using RAL.AST;
namespace RAL.Interpreter;

// One concrete resource assignment satisfying a single spec.
using Selection        =  HashSet<ResourceVal>;

// All valid selections for one spec - List<Selection>
using SelectionsList   =  List<HashSet<ResourceVal>>;


/*
reserve/check:           _room205    and    2 Room                        and  ...      time    condition

spec:                    _room205           2 Room                             ...

selection:              {_room205}         {r2, r4}                            ... 

selectionsList:       [ {_room205} ]     [ {r2, r4}, {r2, r3}, {r3, r4} ]      ... 

selectionsPerSpec [ [ {_room205} ]  ,  [ {r2, r4}, {r2, r3}, {r3, r4} ]  ,   ...  ]


--- Cartesian product picks one selection per spec, then Zip attaches each binding 
A candidate crosses spec boundaries: one selection per spec.

candidate1:  [ (null,  {_room205}) , (null, {r2, r4}) , ("drs", {dr1, dr3, dr5}) ]   
candidate2:  [ (null,  {_room205}) , (null, {r2, r3}) , ("drs", {dr1, dr3, dr5}) ]
            ...
*/

/// <summary> Evaluates an atomic query an returns all valid resource tupples. (Availability, Reservation)
/// Atomic - recurrence has been pulled out. 
/// Specs against time -> crossSpec tupples -> filter by condition -> flatten to output </summary>
internal static class QueryEvaluator {

    internal static IEnumerable<List<ResourceVal>> EvaluateQuery(ResolvedQuery query, EnvV envV, EnvH envH) {

        // Resolve each resource specification against the requested time window
        (List<SelectionsList>? selectionsPerSpec, List<string?> specBindings) = SpecsToSelectionsOnTime(query, envV, envH);
        
        //Indicates failure - At least one spec had no available resources at the requested time
        if (selectionsPerSpec == null)
            return [];

        /* Cartesian product across specs. 

        Candidate == Cross spec tupple, with each element being one selection per specification. 
            r and 2 rc -> 2-tupple, each element is a set, as 2 rc requires 2 resources.

        Outer IEnumarable: all candidates                  [                          , ...]
        List             : one candidate (n-tupple)          [ {_room205} , {r2, r4} ]
        Selection        : a set of resources for one spec     {_room205}
        
        Cartesian product: All possible combinations selections bewteen specs     

            [           
                [ {_room205} , {r2, r4} ] , 
                [ {_room205} , {r2, r3} ] , 
                [ {_room205} , {r3, r4} ]    
            ] 
        */ 
        IEnumerable<List<Selection>> candidates = CartesianProduct(selectionsPerSpec);

        // Discard each candidate tupple, where the same resource appears in more than one element [_room205, _room205,  ]
        candidates = candidates.Where(HasNoResourceOverlap);

        
       // Filter by the query condition, if present
       if (query.Condition != null)
            candidates = candidates.Where(candidate => SatisfiesCondition(candidate, specBindings, query.Condition, envV, envH));
       

        // Grouping by spec no longer needed for id binding - flatten permanently into the output format
        return candidates
                .Select(
                    candidate => candidate.SelectMany(r => r).ToList()
                );
    }
    
    /* ___________________________________________________
    *   Step 1: Resolve resource specs against the time window, bindings parallel list
    * __________________________________________________________*/  

    /// <summary> Resolves each resource spec 're' in (re and re and re...) against the requested time window. </summary>
    /// <param name="query">re and re t.</param>
    /// <returns>(   [ [{_room205}] , [{r2, r4}, {r2, r3}, {r3, r4}] , ... ]  ,  [null, r, ...]   )</returns>
    private static (List<SelectionsList>?, List<string?>) SpecsToSelectionsOnTime(ResolvedQuery query, EnvV envV, EnvH envH) {

        //Parallel lists
        List<SelectionsList> selectionsPerSpec = new();
        List<string?> specBindings = new();

         // Step 1: Find all valid selections for each ResourceSpec (r | a rc[id])
        foreach (ResourceSpec spec in query.ResourceSpecs) {
            
            // for one ResourceSpec (r | a rc[id])
            (SelectionsList? selections, string? specBinding) = spec switch {
                
                //Case 1 - named resourceId: i.e. room204
                ResourceInstanceSpec instanceSpec => InstanceSpecToSelection(instanceSpec, query.Start, query.End, envV),
                
                //Case 2 & 3: (a rc), (a rc id) respectively
                CategorySpec categorySpec => CategorySpecToSelections(categorySpec, query.Start, query.End, envV, envH),

                _ => throw new Exception($"Invalid resource specification.")
            };

            //Any spec failing availability fails the whole query (re and re and re...)
            if (selections == null)
                return (null, []);

            selectionsPerSpec.Add(selections);
            specBindings.Add(specBinding);
        }
        //If this is reached, each spec is satisfied based solely on the given timeslot (condition yet to be evaluated)
        return (selectionsPerSpec, specBindings);
    }
    
    private static (SelectionsList?, string?) InstanceSpecToSelection(ResourceInstanceSpec spec, DateTime requestedStart, DateTime requestedEnd, EnvV envV) {
        
        //lookup in envV - ResourceVal guaranteed by typechecker
        ResourceVal resource = (ResourceVal)envV.Lookup(spec.ResourceId);

        // Required named instance not available at given time. Whole query fails fast.
        if (! ReservationRegistry.Instance().IsAvailable(resource, requestedStart, requestedEnd) )
            return (null, null);

        /*Available - Exactly one selection (itself), wrapped for uniform treatment with CategorySpec*/
        return ([ [resource] ], null); //local binding: impossible by concrete syntax
    }

    private static (SelectionsList?, string?) CategorySpecToSelections(CategorySpec catSpec, DateTime requestedStart, DateTime requestedEnd, EnvV envV, EnvH envH) {
        //extract a
        int quantity = (int)((NumberVal)Interpreter.EvalExp(catSpec.Quantity, envV, envH)).Value;

        /*Extract localBinding if any.
         * as - like a typecast, but returns null if not possible, instead of throwing exception
         * ?. - null-conditional op: member access if operand non-null */
        string? specBinding = (catSpec as CategorySpecWithBinding)?.LocalBindingId;

        IEnumerable<string> subCategories = envH.GetSubCategories(catSpec.CategoryId);
        
        /*A flat set of all available(time) resources from the category and downwards subtree
        * "2 Room r" -> {r1, r2, dr1} */
        HashSet<ResourceVal> availableResources =
            ResourceRegistry.Instance()
                .GetAllResourcesInCategorySubtree(subCategories)
                .Where(resource => ReservationRegistry.Instance().IsAvailable(resource, requestedStart, requestedEnd))
                .ToHashSet();

        /*All size 'a' subsets of available resources:  {r1, r2, dr1} -> [ {r1, r2}, {r1, dr1}, {r2, dr1} ] */
        SelectionsList selections = GetCombinations(availableResources.ToList(), quantity);

        return selections.Count == 0 // existance of size 'a' subsets, determines wether failed or not
            ? (null, null)
            : (selections, specBinding);
    }

/*____________________________________________________________________________________________
 *   Step 2: Build cross-spec candidate tuples, without duplicate resources between its elements. If a resource is used to satisfy one spec already, it cannot be used to satisfy another
 *___________________________________________________________________________________________*/

    /* reserve                     _room205   and   2 Room          [time] [condition]
     * e.g. crossSpecCandidate: [  [_room205] , [_room205, r4]  ] 
     * Both valid candidates isolated to their spec,sShould however get rejected, as the same resource can't be booked twice accross "and" at same time */
    private static bool HasNoResourceOverlap(List<HashSet<ResourceVal>> candidate) {

        // [ _room205, _room205, r4 ]        <- [   {_room205} , {_room205, r4}  ]
        List<ResourceVal> flatCandidateTupple =  candidate.SelectMany(resources => resources).ToList();

        // |[ _room205 , r4]|   !=    |[ _room205, _room205, r4  ]|    i.e. duplicate elements within tuple 
        return flatCandidateTupple.Distinct().Count() == flatCandidateTupple.Count;
        
        // Comparing amount of unique elements in list to amount of actual elements. If they differ, duplicates were present)
    }
    
 /* _________________________________________________________
     Step 3: Filter by condition
    ______________________________________________________*/

/// <summary>
    /// Returns true if the condition holds for this cross-spec candidate tuple.
    /// </summary>
    private static bool SatisfiesCondition(
        List<Selection> candidate, //[]
        List<string?> specBindings,
        Exp condition, EnvV envV, EnvH envH)
    {
        //leverages the parallel lists: candidates ith element (a selection for one spec) would have as a binding, the ith element of the specBindings list
        List<(Selection selection, string? binding)> selectionsWithBindings 
        = candidate.Zip(specBindings, (selection, binding) => (selection, binding))
                    .Where(specElem => specElem.binding != null)
                    .ToList();
        
        //No resource specs had bindings in the entire re
        if (selectionsWithBindings.Count == 0)
            return EvaluateCondition(condition, envV, envH);
        
        // Each bound spec: one (name, resource) pair per resource in its selection.
        // ("dr", [d1,d2]) -> [ ("dr",d1), ("dr",d2) ]
        IEnumerable<IEnumerable<(string, ResourceVal)>> perSpec 
        = selectionsWithBindings.Select(tuple => tuple.selection.Select(resource => (tuple.binding!, resource)));

        // Cartesian product: all combinations of one resource per bound spec.
        foreach (List<(string name, ResourceVal resource)> combination in CartesianProduct(perSpec)) {
            EnvV scope = envV.NewScope();
            foreach ((string name, ResourceVal resource) in combination)
                scope.Bind(name, resource);

            if (!EvaluateCondition(condition, scope, envH))
                return false;
        }

        return true;
    }

  private static bool EvaluateCondition(Exp condition, EnvV envV, EnvH envH) {
        try {
            return Interpreter.EvalExp(condition, envV, envH).AsBool();
        } catch (Interpreter.MissingPropertyException) {
            return false;
        }
    }

 /*__________________________________________
*   utilities
*  ______________________________________*/

    /// <summary> All combinations of exactly <paramref name="n"/> resources from <paramref name="resources"/>. </summary>
    private static List<Selection> GetCombinations(List<ResourceVal> resources, int n) {
        
        //Recursion base case: choosing 0 resources always yields exactly one valid result - the empty selection
        if (n == 0) return [[]];

        //Base case: not enough resources left to fill n slots - no valid combinations possible
        if (resources.Count < n) return [];

        /* Base case: exactly n resources remain - only one possible selection, take them all.  [..resources] spread operator: unpack all elements of items into a new collection
          i.e. [..resources] where resources=[r1,r2] produces a new HashSet {r1, r2}*/
        if (resources.Count == n) return [ [..resources] ];

        //Recursive case: more resources left than spots to fill
        ResourceVal       head = resources[0];
        List<ResourceVal> tail = resources.Skip(1).ToList(); // everything except head

        /* Include head: choose k-1 more from tail, then add head to each result
         Combinations([r2,r3], 1) -> [ [r2], [r3] ]
         after attaching head r1  -> [ {r1,r2}, {r1,r3} ]
         combo.Append(head) produces an IEnumerable - ToHashSet() collects it into a Selection
         [..items] inside a HashSet literal: spread unpacks combo's elements, head adds one more*/
        List<Selection> withHead = GetCombinations(tail, n - 1)
                                        .Select(combo => new Selection([..combo, head]))
                                        .ToList();

        /* Exclude head: choose all k from tail, head is never considered again
          Combinations([r2,r3], 2) -> [ {r2,r3} ]*/
        List<Selection> withoutHead = GetCombinations(tail, n);

        /* Merge both branches
        // [..withHead, ..withoutHead]: spread both lists into a single new list
        // equivalent to withHead.Concat(withoutHead).ToList() */
        return [..withHead, ..withoutHead];
    }

    /// <summary> Generates the Cartesian product of a sequence of sequences. </summary>
    private static IEnumerable<List<T>> CartesianProduct<T>(IEnumerable<IEnumerable<T>> sequences) {
        IEnumerable<List<T>> result = new List<List<T>> { new List<T>() };

        foreach (IEnumerable<T> sequence in sequences) {
            result =
                from partial in result
                from item in sequence
                select partial.Append(item).ToList();
        }

        return result;
    }
}