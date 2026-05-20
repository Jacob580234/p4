namespace RAL.Interpreter;

using System.Globalization;

/* Runtime values used by the interpreter.

 * Wraps c# typed values in record classes to have one common super "Value" as return type for EvalExp.

 *ToString method of each record type overridden to the underlying value's ToString method
*/

/* Context:
 These values are different from AST nodes:
 - AST nodes describe the source program.
 - Runtime values are the result of evaluating expressions.
*/
public abstract record Value {
    /*Default methods to avoid poluting interpreter.cs with downcasts. 
      Typechecking should have guarded possible exceptions */
    public bool AsBool()  => ((BoolVal)this).Value; 
    
}

/* For now, Number is represented as float because the language 
   uses one Number type for both integer-like and decimal values. */
record NumberVal(float Value) : Value {
    // "G" keeps the output compact, e.g. 2 instead of 2.000000
    public override string ToString() => Value.ToString("G");
}

record BoolVal(bool Value) : Value {
    // Match common DSL syntax: true / false instead of True / False
    public override string ToString() => Value.ToString().ToLower();
}

record StringVal(string Value) : Value {
    public override string ToString() => Value;
}

/*________________ Time ______________*/
public record DateTimeVal(DateTime Value) : Value {
    // Returns the DateTime formatted as custom "dd/MM-yyyy HH:mm" using French (fr-FR) culture.
    public override string ToString() => Value.ToString("dd/MM-yyyy HH:mm", new CultureInfo("fr-FR"));
}

public record DurationVal(TimeSpan Value) : Value {
    public override string ToString() => Value.ToString();
}

//EnvV, EnvF, EnvH

//CategoryId for move to avoid linear search of every list in Categories,     property id -> value
public record ResourceVal(string ResourceId, string CategoryId, Dictionary<string , Value> Properties) : Value {
    public string CategoryId {get; set;} = CategoryId; // this.CategoryId set to parameter CategoryId
    
    public override string ToString() {
        
        string props = string.Join(
            ", " , 
            Properties.Select(p => $"{p.Key}: {p.Value}") );

        return $"{CategoryId} '{ResourceId}' [{props}] ";
    }
}

// 2 DoubleRoom dr and 3 Room ... time 
//Alternative one resourceVal and make it composite reservation i.e. ReservationVal, drawback cannot reschedule
public record ReservationAtomVal( List<ResourceVal> Resources, DateTimeVal Start, DateTimeVal End ) {
    public DateTimeVal Start {get; set;} = Start;
    public DateTimeVal End {get; set;} = End;

    public override string ToString() {
        string resources = string.Join("\n    - ", Resources.Select(r => r.ToString()));

        return $"    Time: {Start} -> {End}\n    Resources:\n    - {resources}";
    }
}

/// <summary> All reservations are treated equally like this. 
/// An atomic reservation, has a list of length 1.
/// An empty list indicates a "failed" reservation attempt. </summary>
public record ReservationVal( List<ReservationAtomVal> Reservations) : Value {

    public bool Failed() { return this.Reservations.Count == 0; }

    public bool IsComposite() { return this.Reservations.Count > 1; }

    public override string ToString() {
        if (Failed())
            return "FAILED RESERVATION";

        return string.Join(
            "\n\n",
            Reservations.Select((reservationAtom, i) => $"  Reservation Atom #{i + 1}\n{reservationAtom}")
        );
    }
}