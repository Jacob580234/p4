using RAL.AST;
using RAL.TC;
using System.Windows;

namespace RAL.Interpreter;

class Program {
    static void Main(string[] args) {

        //Ensure RAL src code is provided
        if (args.Length < 1) {
            PrintUsage();
            Environment.Exit(1); // indicates abnormal program termination (failure)
        }

        string filePath = args[0];

        if (!File.Exists(filePath)) {
            Console.WriteLine($"File not found: {filePath}\n");
            Environment.Exit(1); // indicates abnormal programm termination (failure)
        }

        //Begin parsing
        Parser parser = new Parser(new Scanner(filePath));
        parser.Parse();

        //Stop at syntactic errors
        if (parser.errors.count > 0) {

            Console.WriteLine($"Parsing failed with {parser.errors.count} error(s).");
            Environment.Exit(1);
        }

        //No syntactic errors, extract AST
        Stmt program = parser.mainNode ?? throw new NullReferenceException();
        
        //Type-checking, entry-point is Stmt
        TypeChecker typeChecker = new TypeChecker();
        typeChecker.StmtType(
            program, 
            new TC.EnvV(), 
            new EnvC(), 
            new TC.EnvH(), 
            new EnvT(), 
            new EnvR(), 
            new EnvCPT()
        );
        
        //Stop at type-checking errors
        if (typeChecker.errors.Count > 0) {
            foreach (string error in typeChecker.errors) 
                Console.WriteLine(error + "\n");
            
            Console.WriteLine($"Program exited with {typeChecker.errors.Count} errors in type checking.");
            Environment.Exit(1);
        } 
        
        //No errors in front-end analysis, interprete program
        Interpreter.ExecStmt(program, new EnvV(), new EnvH(), new EnvTem());
    

        Console.WriteLine(ResourceRegistry.Instance().ToString());
        Console.WriteLine(ReservationRegistry.Instance().ToString());
        
    }

    static void PrintUsage() 
    {
        Console.WriteLine("Usage: dotnet run Program.cs <ral-source-file>");
        Console.WriteLine("       dotnet test");
    }
}