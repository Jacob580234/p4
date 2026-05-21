using RAL.AST;

namespace RAL.Tests;

/*
 * Test level: unit (parser only — no type or interpreter assertions).
 *
 * Tests that verify only syntax and basic AST-shape concerns.
 * No type or runtime assertions belong here.
 *
 * Scope:
 *   - The parser must reject obvious syntax errors.
 *   - The parser must accept the small number of grammar forms that no AST
 *     or TypeCheck test happens to exercise on the same source.
 *   - One structural-shape test pins the root layout of a VarDecl + initialiser.
 *
 * Tests asserting that "valid program X parses without errors" were removed:
 *   the AST and TypeCheck tests that consume those same sources already invoke
 *   ParseShouldSucceed internally, so parser coverage is implicit.
 */
public class ParserTests
{
    // ── Invalid syntax must produce parse errors ─────────────────────────────

    [Fact]
    public void MissingSemicolon_IsRejectedByParser()
    {
        TestHelpers.ParseShouldFail(TestPrograms.InvalidSyntaxMissingSemicolon);
    }

    [Fact]
    public void BadDeclaration_IsRejectedByParser()
    {
        TestHelpers.ParseShouldFail(TestPrograms.InvalidSyntaxBadDecl);
    }

    [Fact]
    public void UnclosedParenthesis_IsRejectedByParser()
    {
        TestHelpers.ParseShouldFail(TestPrograms.InvalidSyntaxUnclosedParen);
    }

    // ── Empty input must parse ───────────────────────────────────────────────

    [Fact]
    public void EmptyProgram_ParsesSuccessfully()
    {
        // An empty file is a valid RAL program (zero statements).
        TestHelpers.ParseShouldSucceed("");
    }

    // ── Root AST node shape ──────────────────────────────────────────────────

    [Fact]
    public void NumberDecl_RootIsCompositeOfVarDeclAndAssignmentExpStmt()
    {
        // "Number x = 42;" → Composite(VarDecl, ExpStmt(Assignment)).
        // One test pins the full shape; previously split across three.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidNumberDecl);
        var composite = Assert.IsType<Composite>(root);
        Assert.IsType<VarDecl>(composite.Stmt1);
        var expStmt = Assert.IsType<ExpStmt>(composite.Stmt2);
        Assert.IsType<Assignment>(expStmt.Expression);
    }

    // ── Grammar forms not exercised by any AST/TypeCheck test ────────────────

    [Fact]
    public void ValidDateTimeWithTime_ParsesSuccessfully()
    {
        // Date + optional time component — both parts must be recognised by the scanner.
        TestHelpers.ParseShouldSucceed(TestPrograms.ValidDateTimeWithTime);
    }

    [Fact]
    public void ValidDuration_CompoundUnit_ParsesSuccessfully()
    {
        // Combined units: 1 week 2 days 3 hours 30 minutes.
        TestHelpers.ParseShouldSucceed(TestPrograms.ValidDurationCompound);
    }

    [Fact]
    public void ReserveWithForDuration_ParsesSuccessfully()
    {
        // "for Duration" alternative in the Time non-terminal.
        TestHelpers.ParseShouldSucceed(TestPrograms.ValidReserveForDuration);
    }

    [Fact]
    public void ReserveWithQuantityAndCategory_ParsesSuccessfully()
    {
        // "a*rc" resource-spec form: "2 Room", no alias.
        TestHelpers.ParseShouldSucceed(TestPrograms.ValidReserveQuantityCategory);
    }

    [Fact]
    public void ReserveWithWhereClause_ParsesSuccessfully()
    {
        // Optional "where (Exp)" predicate on a "1 Room r" spec.
        TestHelpers.ParseShouldSucceed(TestPrograms.ValidReserveWherePredicate);
    }

    // ── Malformed domain-specific syntax ─────────────────────────────────────

    [Fact]
    public void ReserveMissingFrom_IsRejectedByParser()
    {
        // "reserve myRoom 15/03-2026 to 16/03-2026" — Time non-terminal requires
        // the "from" keyword before the start expression. Without it the parser
        // tries to read another identifier where a dateLit appears, and errors.
        TestHelpers.ParseShouldFail(TestPrograms.InvalidSyntaxReserveMissingFrom);
    }

    [Fact]
    public void CheckMissingToOrFor_IsRejectedByParser()
    {
        // "check myRoom from 15/03-2026;" — after the start DateTime the
        // Time non-terminal requires either "to DateTime" or "for Duration".
        // Hitting ";" instead must produce a parse error.
        TestHelpers.ParseShouldFail(TestPrograms.InvalidSyntaxCheckMissingToOrFor);
    }
}
