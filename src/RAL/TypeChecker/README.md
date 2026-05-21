# TypeChecker

Static type checking over the AST. Collects errors into `TypeChecker.errors` rather than throwing, so the interpreter can decide whether to continue.

## Two entry points

- **`ExpType`** — recurses down an expression, returning the inferred `Type`. Leaves (literals, identifiers) yield a type that bubbles up to operator nodes for compatibility checks.
- **`StmtType`** — walks statements, threading the environments below and validating declarations, assignments, control flow, resources, templates, and queries.

## Environments

| Env     | Purpose                                  |
|---------|------------------------------------------|
| `EnvV`  | Variables → type                         |
| `EnvC`  | Categories and their hierarchy           |
| `EnvCPT`| Category property types                  |
| `EnvR`  | Resources → category                     |
| `EnvT`  | Templates → parameter signatures         |
| `EnvH`  | Lexical scope chain (parent environment) |
