# AST

Node definitions for the RAL abstract syntax tree.

- `Stmt.cs` - statement nodes (declarations, control flow, queries)
- `Exp.cs` - expression nodes (literals, operators, identifiers)
- `Type.cs` - type representations used by the type checker
- `QueryData.cs` - supporting data carried by query statements

Nodes are immutable C# records, leaning on positional parameters and structural equality.

### References
- [Record class declaration](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records#declare-a-record)
- [Positional parameters in derived records](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record#positional-parameters-in-derived-record-types)
- [Primary constructors](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/primary-constructors)
- [Record value equality](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record#value-equality)
