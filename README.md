# RAL - Resource Availability Language

A domain-specific language for declaring resources and querying their availability over time.
Semester project at Aalborg University, SW4 Group 2, 2026.

## Project Structure

```
p4/
├── CocoR/                # Coco/R grammar and generation script
├── src/RAL/
│   ├── Generated/        # Auto-generated scanner and parser (do not edit)
│   ├── AST/              # Abstract syntax tree (statements, expressions, types)
│   ├── TypeChecker/      # Static type checking and environments
│   ├── Interpreter/      # Tree-walking interpreter and runtime registries
│   └── Program.cs        # Entry point
└── tests/RAL.Tests/      # xUnit test suite (parser, type checker, interpreter)
```

## Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Coco/R](https://ssw.jku.at/Research/Projects/Coco/) - only needed when regenerating the parser

## Usage

```bash
git clone https://github.com/NicklasHM/p4
cd p4
dotnet run --project src/RAL -- <inputfile>.ral
```

Run the full test suite:

```bash
dotnet test
```

Regenerate the parser after grammar changes:

```bash
cd CocoR
./generate.ps1
```

This writes `Scanner.cs` and `Parser.cs` into `src/RAL/Generated/`.

## Example

```
category Room;
category DoubleRoom is a Room;
DoubleRoom room205 {
  Bool seaView = true;
  Number floor = 2;
};

check room205 from 15/03-2026 14:00 to 17/03-2026 12:00;
```

## Pipeline

Source -> Scanner -> Parser -> AST -> TypeChecker -> Interpreter -> Resource & Reservation registries.

## Group Members

- Abtin Sedigh Rezvani
- Jacob Alexander Byrdal
- Jacob Kjærsgaard Sand
- Mathias Emborg
- Mihnea Christian Spinu
- Nicklas Holm Mikkelsen
