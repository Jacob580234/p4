using RAL.AST;
namespace RAL.Interpreter;

// One concrete resource assignment satisfying a single spec.
using Selection        =  HashSet<ResourceVal>;

// All valid selections for one spec - List<Selection>
using SelectionsList   =  List<HashSet<ResourceVal>>;

// The valid selections for every spec. One SelectionsList per spec.
using AllSelectionsLists =  List<List<HashSet<ResourceVal>>>; //List<Selectionlist>;

/*
reserve/check:           _room205    and    2 Room                        and  ...      time    condition

spec:                    _room205           2 Room                             ...

selection:              {_room205}         {r2, r4}                            ... 

selectionsList:       [ {_room205} ]     [ {r2, r4}, {r2, r3}, {r3, r4} ]      ... 

all_selectionsLists [ [ {_room205} ]  ,  [ {r2, r4}, {r2, r3}, {r3, r4} ]  ,   ...  ]


--- Cartesian product picks one selection per spec, then Zip attaches each binding 
A candidate crosses spec boundaries: one selection per spec.

candidate:  [ (null,  {_room205}) , (null, {r2, r4}) , ("drs", {dr1, dr3, dr5}) ]   
            [ (null,  {_room205}) , (null, {r2, r3}) , ("drs", {dr1, dr3, dr5}) ]
            ...
*/

/// <summary> Evaluates an atomic query an returns all valid resource tupples. (Availability, Reservation)
/// Atomic - recurrence has been pulled out. 
/// Specs against time -> crossSpec tupples -> filter by condition -> flatten to output </summary>
internal static class QueryEvaluator {

    internal static IEnumerable<List<ResourceVal>> EvaluateQuery(ResolvedQuery query, EnvV envV, EnvH envH) {

        // Resolve each resource specification against the requested time window
        List<ResolvedSpec>? all_selectionsLists = SpecsToSelectionsOnTime(query, envV, envH);
        
        //Indicates failure - At least one spec had no available resources at the requested time
        if (all_selectionsLists == null)
            return [[]];

        // Across spec boundary: cartesian product + binding attachment -> candidates (List
        //Candidates are now tuples of: one candidate from each spec's selectionsList. Discards tuples with overlapping resources between its elements.
        IEnumerable<List<SpecSelection>> candidates = BuildCandidateTupples(all_selectionsLists);

       // Filter by the query condition, if present
        IEnumerable<List<SpecSelection>> validCandidates = 
            query.Condition == null
                ? candidates
                : candidates.Where(candidate => CandidateSatisfiesCondition(candidate, query.Condition, envV, envH));

         // Grouping by spec no longer needed for id binding — flatten permanently into the output format
        return validCandidates
                .Select(
                    crossSpecTuple => crossSpecTuple.SelectMany(specSelection => specSelection.Resources).ToList()
                );
    }
    
    /* ___________________________________________________
    *   Step 1: Resolve resource specs against the time window 
    * __________________________________________________________*/

    /// <summary> Resolves each resource spec 're' in (re and re and re...) against the requested time window. </summary>
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

/*_______________________________________________________
 *   Step 2+3: Build cross-spec candidate tuples, without duplicate elements
 *________________________________________________________________*/

  private static IEnumerable<List<SpecSelection>> BuildCandidateTupples(List<ResolvedSpec> resolvedSpecs) {

        //Temporarily discards binding ids, and keeps only the raw selections for each spec.
        AllSelectionsLists selectionsPerSpec = 
            resolvedSpecs
                .Select(resolvedSpec => resolvedSpec.ValidSelections)
                .ToList();

        /* Cartesian product across specs.

        Candidate == Cross spec tupple, with one selection per specification. 
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
        IEnumerable<List<Selection>> crossings = CartesianProduct(selectionsPerSpec);

        IEnumerable<List<SpecSelection>> candidates = crossings.Select(
            crossing => crossing.Zip(
                resolvedSpecs, (selection, spec) => new SpecSelection(selection, spec.LocalBinding)
                )
                .ToList() 
        );

        // Discard candidates where the same resource appears in more than one element [_room205, _room205,  ]
        return candidates.Where(HasNoResourceOverlap);
    }

    /* reserve                     _room205   and   2 Room          [time] [condition]
     * e.g. crossSpecCandidate: [  [_room205] , [_room205, r4]  ] 
     * Both valid candidates isolated to their spec,sShould however get rejected, as the same resource can't be booked twice accross "and" at same time */
    private static bool HasNoResourceOverlap(List<List<ResourceVal>> crossSpecCandidate) {

        // [ _room205, _room205, r4 ]        <- [   [_room205] , [_room205, r4]  ]
        List<ResourceVal> flatCandidateTupple =  crossSpecCandidate.SelectMany(resources => resources).ToList();

        // |[ _room205 , r4]|   !=    |[ _room205, _room205, r4  ]|    i.e. duplicate elements within tuple 
        return flatCandidateTupple.Distinct().Count() == flatCandidateTupple.Count;
        
        // Comparing amount of unique elements in list to amount of actual elements. If they differ, duplicates were present)
    }

//---------------------------------Condition
// Step 4: Evaluate Condition {}
/*        all_distinctCandidateTuples = all_distinctCandidateTuples.Where(assignment => {
            var bindingSets = new List<KeyValuePair<string, List<ResourceVal>>>();
            for (int i = 0; i < assignment.Count; i++) {
                if (all_selectionsLists[i].LocalBinding != null) {
                bindingSets.Add(new KeyValuePair<string, List<ResourceVal>>(
                    all_selectionsLists[i].LocalBinding!,
                    assignment[i]));
                }
            }

            if (!bindingSets.Any()) {
                // No bound variables; evaluate once
                try {
                    return Interpreter.EvalExp(query.Condition, envV, envH).AsBool();
                } catch (Interpreter.MissingPropertyException) {
                    return false;
                }
            }

            // Generate Cartesian product of individual resources for the bound variables
            var elementSets = bindingSets.Select(kvp => kvp.Value.Select(r => new KeyValuePair<string, Value>(kvp.Key, r)));
            var bindingCombinations = CartesianProduct(elementSets);

            // The condition must be true for EVERY element combination
            foreach (var bindCombo in bindingCombinations) {
                EnvV scope = envV.NewScope();
                foreach (var binding in bindCombo) {
                    scope.Bind(binding.Key, binding.Value);
                }
                try {
                    if (!Interpreter.EvalExp(query.Condition, scope, envH).AsBool()) {
                        return false;
                    }
                } catch (Interpreter.MissingPropertyException) { 
                    return false;
                }
            }
            return true;
        });*/

    
 /* _________________________________________________________
     Step 4: Filter by condition
    ______________________________________________________*/

/// <summary>
    /// Returns true if the condition holds for this cross-spec candidate tuple.
    /// When a spec carries a local binding (e.g. <c>3 DoubleRoom drs</c>), the condition
    /// is evaluated once per individual resource in that candidate — and must hold for all of them.
    /// </summary>
    private static bool CandidateSatisfiesCondition(
        List<SpecSelection> crossSpecCandidateTuple,
        Exp condition,
        EnvV envV,
        EnvH envH)
    {
        Dictionary<string, Selection> boundVars = crossSpecCandidateTuple
            //Filter those that have a local binding
            .Where(specSelection => specSelection.LocalBinding != null)
            .ToDictionary(
                //localBinding -> the selection of resources it binds
                specSelection => specSelection.LocalBinding!, 
                specSelection => specSelection.Resources
            );


        return boundVars.Count == 0
            ? EvaluateCondition(condition, envV, envH)
            : ConditionHoldsForAllBindings(condition, boundVars, envV, envH);
    }

    /// <summary> Pairs each local binding name with the candidate chosen for its spec. </summary>
    private static Dictionary<string, Selection> CollectBoundVariables(
        List<List<ResourceVal>> crossSpecCandidateTuple,
        List<ResolvedSpec> all_selectionsLists)
    {
        Dictionary<string, Selection> bindings = new();
        for (int i = 0; i < all_selectionsLists.Count; i++) {
            if (all_selectionsLists[i].LocalBinding is string name)
            // being apart of the ith Candidate Set, means being the ith element in the cross spec tupple. 
                bindings.Add((name, crossSpecCandidateTuple[i]));
        }
        return bindings;
    }

    /// <summary>
    /// For a binding like <c>3 DoubleRoom drs</c>, the chosen candidate is e.g. [dr1, dr3, dr5].
    /// The condition must hold with drs=dr1, drs=dr3, and drs=dr5 individually — and for
    /// every combination across all bound specs. Cartesian product covers the multi-binding case.
    /// </summary>
    private static bool ConditionHoldsForAllBindings(
        Exp condition,
        Dictionary<string, List<ResourceVal>> bindings,
        EnvV envV,
        EnvH envH)
    {
        // For each binding, produce one (name, resource) pair per resource in its candidate
        IEnumerable<IEnumerable<(string, ResourceVal)>> perBindingElements =
            bindings.Select(b => b.BoundResources.Select(resource => (b.BindingName, resource)));

        // Cartesian product: all combinations of one element per bound variable
        IEnumerable<List<(string, ResourceVal)>> elementCombinations = CartesianProduct(perBindingElements);

        // Condition must hold for every combination
        foreach (List<(string bindingName, ResourceVal resource)> combination in elementCombinations) {
            EnvV scope = envV.NewScope();
            foreach ((string bindingName, ResourceVal resource) in combination)
                scope.Bind(bindingName, resource);

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
        if (resources.Count < n) return [[]];


        // Base case: exactly n resources remain - only one possible selection, take them all.  [..resources] spread operator: unpack all elements of items into a new collection
        // i.e. [..resources] where resources=[r1,r2] produces a new HashSet {r1, r2}
        if (resources.Count == n) return [ [..resources] ];

        //Recursive case: more resources left than spots to fill
        ResourceVal       head = resources[0];
        List<ResourceVal> tail = resources.Skip(1).ToList(); // everything except head

        // Include head: choose k-1 more from tail, then add head to each result
        // Combinations([r2,r3], 1) -> [ [r2], [r3] ]
        // after attaching head r1  -> [ {r1,r2}, {r1,r3} ]
        // combo.Append(head) produces an IEnumerable — ToHashSet() collects it into a Selection
        // [..items] inside a HashSet literal: spread unpacks combo's elements, head adds one more
        List<Selection> withHead = GetCombinations(tail, n - 1)
                                        .Select(combo => new Selection([..combo, head]))
                                        .ToList();

        // Exclude head: choose all k from tail, head is never considered again
        // Combinations([r2,r3], 2) -> [ {r2,r3} ]
        List<Selection> withoutHead = GetCombinations(tail, n);

        // Merge both branches
        // [..withHead, ..withoutHead]: spread both lists into a single new list
        // equivalent to withHead.Concat(withoutHead).ToList() 
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