using System.IO;
using System.Reflection;
using System.Text;
using RAL.AST;
using RAL.TC;
using RAL.Interpreter;
using Interp = RAL.Interpreter.Interpreter;

namespace RAL.Tests;

/*
 * Reusable helpers for all test classes.
 * Each helper either drives the real production pipeline
 * (Parser → TypeChecker → Interpreter) or constructs AST nodes
 * directly for lower-level tests.
 *
 * Intended error-reporting contract for the TypeChecker:
 *   All semantic/type errors must be reported through tc.errors.Add(...).
 *   The typechecker must NOT throw exceptions for detectable semantic errors.
 *   These helpers do NOT catch typechecker exceptions.
 *   If the typechecker throws, the calling test FAILS — that failure is the signal
 *   that the typechecker needs to be fixed to use tc.errors instead of throwing.
 */
static class TestHelpers
{
    // ── Parser helpers ──────────────────────────────────────────────────────

    // Parse source using the real CoCo/R Scanner + Parser.
    // Parser error output is silenced so tests stay clean.
    public static Parser ParseProgram(string source)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        var stream = new MemoryStream(bytes);
        var scanner = new Scanner(stream);
        var parser = new Parser(scanner);
        parser.errors.errorStream = TextWriter.Null;
        parser.Parse();
        return parser;
    }

    // Assert that source parses without errors and return the root AST node.
    public static Stmt ParseShouldSucceed(string source)
    {
        Parser parser = ParseProgram(source);
        Assert.Equal(0, parser.errors.count);
        Assert.NotNull(parser.mainNode);
        return parser.mainNode!;
    }

    // Assert that source produces at least one parse error.
    public static void ParseShouldFail(string source)
    {
        Parser parser = ParseProgram(source);
        Assert.True(parser.errors.count > 0,
            "Expected parse errors but the parser reported none.");
    }

    // ── TypeChecker helpers ─────────────────────────────────────────────────

    // Parse and typecheck; assert zero type errors and no exception.
    // If the typechecker throws, the test fails — semantic errors must use tc.errors.
    public static TypeChecker TypeCheckShouldSucceed(string source)
    {
        Stmt node = ParseShouldSucceed(source);
        var tc = RunTypeChecker(node);
        Assert.Empty(tc.errors);
        return tc;
    }

    // Parse and typecheck; assert at least one type error and no exception.
    // If the typechecker throws, the test fails — semantic errors must use tc.errors.
    public static TypeChecker TypeCheckShouldFail(string source)
    {
        Stmt node = ParseShouldSucceed(source);
        var tc = RunTypeChecker(node);
        Assert.NotEmpty(tc.errors);
        return tc;
    }

    // Parse and typecheck; assert at least one error and optionally verify that at
    // least one error message contains one of the supplied keywords (case-insensitive).
    // If the typechecker throws, the test fails — semantic errors must use tc.errors.
    public static TypeChecker TypeCheckShouldReportError(string source, params string[] expectedKeywords)
    {
        Stmt node = ParseShouldSucceed(source);
        var tc = RunTypeChecker(node);
        Assert.NotEmpty(tc.errors);
        if (expectedKeywords.Length > 0)
        {
            Assert.Contains(tc.errors, e =>
                expectedKeywords.Any(kw => e.Contains(kw, StringComparison.OrdinalIgnoreCase)));
        }
        return tc;
    }

    // Run the TypeChecker on an already-parsed node with fresh, empty environments.
    // Does NOT catch exceptions — if the typechecker throws, the caller's test fails.
    public static TypeChecker RunTypeChecker(Stmt node)
    {
        var tc = new TypeChecker();
        tc.StmtType(node, new TC.EnvV(), new EnvC(), new TC.EnvH(), new EnvT(), new EnvR(), new EnvCPT());
        return tc;
    }

    // ── Interpreter helpers ─────────────────────────────────────────────────

    // Evaluate an expression using the real Interpreter and return the result.
    public static Value EvalExpression(Exp exp, Interpreter.EnvV envV, EnvH envH)
    {
        return Interp.EvalExp(exp, envV, envH);
    }

    // ── Singleton reset for test isolation ──────────────────────────────────
    //
    // ResourceRegistry and ReservationRegistry are process-wide singletons.
    // Without resetting between tests, registrations from one test leak into the
    // next — forcing every test to invent unique category names (RoomA, RoomB…).
    // Reflection is used so production code stays untouched.
    // Each reset asserts the private field exists; a rename in production will
    // surface as a clear test-helper failure rather than spooky cross-test state.

    public static void ResetRegistries()
    {
        ResetResourceRegistry();
        ResetReservationRegistry();
    }

    private static void ResetResourceRegistry()
    {
        var instance = ResourceRegistry.Instance();
        FieldInfo field = typeof(ResourceRegistry).GetField(
            "_registry", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "ResourceRegistry._registry field not found — has it been renamed?");
        var dict = (Dictionary<string, HashSet<ResourceVal>>)field.GetValue(instance)!;
        dict.Clear();
        dict.Add("Resource", new HashSet<ResourceVal>());
    }

    private static void ResetReservationRegistry()
    {
        var instance = ReservationRegistry.Instance();
        FieldInfo field = typeof(ReservationRegistry).GetField(
            "_registry", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "ReservationRegistry._registry field not found — has it been renamed?");
        var set = (HashSet<ReservationVal>)field.GetValue(instance)!;
        set.Clear();
    }

    // ── AST inspection helpers ──────────────────────────────────────────────

    // Walk the composite-statement tree that VarDecl+initializer produces and
    // return the expression on the right-hand side of the first Assignment node found.
    public static Exp ExtractFirstAssignmentRhs(Stmt root)
    {
        return TryFindAssignmentRhs(root)
            ?? throw new InvalidOperationException(
                "No Assignment expression found in the AST.");
    }

    private static Exp? TryFindAssignmentRhs(Stmt? stmt)
    {
        if (stmt is null) return null;
        if (stmt is ExpStmt { Expression: Assignment a }) return a.Expression;
        if (stmt is Composite c)
            return TryFindAssignmentRhs(c.Stmt1) ?? TryFindAssignmentRhs(c.Stmt2);
        return null;
    }

    // Walk the composite-statement tree and return the RHS of the Nth Assignment
    // found in left-to-right order (1-indexed).
    public static Exp ExtractNthAssignmentRhs(Stmt root, int n)
    {
        int counter = 0;
        return TryFindNthRhs(root, n, ref counter)
            ?? throw new InvalidOperationException(
                $"Fewer than {n} Assignment expressions found in the AST.");
    }

    private static Exp? TryFindNthRhs(Stmt? stmt, int target, ref int counter)
    {
        if (stmt is null) return null;
        if (stmt is ExpStmt { Expression: Assignment a })
        {
            counter++;
            if (counter == target) return a.Expression;
            return null;
        }
        if (stmt is Composite c)
        {
            Exp? found = TryFindNthRhs(c.Stmt1, target, ref counter);
            return found ?? TryFindNthRhs(c.Stmt2, target, ref counter);
        }
        return null;
    }
}
