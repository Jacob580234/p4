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

    // ── Round-trip: parse → pretty-print → reparse ───────────────────────────
    //
    // Front-end integration test recommended by Thomsen (TestComp.html):
    // "If the pretty printed program looks similar to the input program,
    // there is a high probability that the front end is implemented
    // correctly."
    //
    // The flow inside AssertRoundTripStable is two parses and two prints:
    //   1. Parse the source string from TestPrograms          → AST_1
    //   2. Pretty-print AST_1 via AstPrettyPrinter            → text_1
    //   3. Re-parse text_1 (must succeed)                     → AST_2
    //   4. Pretty-print AST_2                                 → text_2
    //   5. Assert text_1 == text_2
    //
    // The original source is the entry point only. The assertion does NOT
    // compare against it, because the printer normalises information the
    // AST has lost (e.g. the source "1 week" becomes a TimeSpan in the AST,
    // and the printer emits it as "7 days"). What we DO assert is that the
    // printer is unchanged on parser output: once the source has been
    // normalised to text_1, a second round trip must produce the same text.
    //
    // Failure modes and what they mean:
    //   - Step 3 fails: the printer produced text the grammar rejects,
    //     i.e. a printer bug or a grammar form the printer forgot.
    //   - Step 5 fails: either the parser produced a different AST shape on
    //     the second pass (front-end shape drift) or the printer is not
    //     deterministic for the same AST.

    [Fact]
    public void RoundTrip_SimpleDecl_IsStable()
    {
        // "Number x = 42;" is the most common parser-output pattern:
        // Composite(VarDecl, ExpStmt(Assignment)). The printer fuses these
        // two nodes back into the surface form "Type id = expr;". The round
        // trip confirms the fusion is reversible: fuse, reparse, refuse,
        // and the text is identical.
        AssertRoundTripStable(TestPrograms.ValidNumberDecl);
    }

    [Fact]
    public void RoundTrip_ArithmeticPrecedence_IsStable()
    {
        // "Number x = 2 + 3 * 4;" parses to ADD(2, MUL(3, 4)) because MUL
        // binds tighter than ADD. The printer wraps every BinaryOperation in
        // explicit parens — "(2 + (3 * 4))" — so the operator nesting is
        // unambiguous on reparse and does not depend on the grammar's
        // binding-power rules. Failure here points at either a parser
        // precedence regression or a printer paren-placement bug.
        AssertRoundTripStable(TestPrograms.ValidNestedArithmetic);
    }

    [Fact]
    public void RoundTrip_ReserveStatement_IsStable()
    {
        // A full domain-specific program: a CategoryDecl, a ResourceDecl
        // with an empty body, and a Reserve expression carrying a named
        // resource and a date interval. Exercises the RAL-specific grammar
        // pieces (Reserve / QueryData / TimeSpec / ResourceSpec / empty
        // ResourceDecl) that the two generic-expression round-trips above
        // do not touch.
        AssertRoundTripStable(TestPrograms.ValidReserveStatement);
    }

    // ── Extended round-trip coverage ─────────────────────────────────────────
    //
    // Each test below pins one further grammar production that the three
    // round-trips above do not reach. The tests intentionally assert the
    // strict contract (parse → print → reparse → print must be a byte-stable
    // fixed point). A failure here is a real signal — typically that the
    // printer is non-deterministic on a given AST shape, or that printer and
    // parser shapes have drifted apart for that production. The right
    // response to a failure is to fix the underlying drift, not to weaken
    // the assertion.

    [Fact]
    public void RoundTrip_CategoryHierarchy_IsStable()
    {
        // Exercises CategoryDecl with an "is a" parent reference, which the
        // simpler ValidCategory program does not cover.
        AssertRoundTripStable(TestPrograms.ValidCategoryHierarchy);
    }

    [Fact]
    public void RoundTrip_ResourceWithProperty_IsStable()
    {
        // ResourceDecl with a non-empty property list. The printer must emit
        // the fused "Type id = expr;" form inside the braces and the parser
        // must accept it as a property declaration.
        AssertRoundTripStable(TestPrograms.ValidResourceWithProperty);
    }

    [Fact]
    public void RoundTrip_ReserveForDuration_IsStable()
    {
        // "from DateTime for Duration" — the alternative TimeSpec form that
        // forces the printer's endKw branch to pick "for" instead of "to".
        AssertRoundTripStable(TestPrograms.ValidReserveForDuration);
    }

    [Fact]
    public void RoundTrip_ReserveQuantityCategory_IsStable()
    {
        // CategorySpec without an alias ("2 Room"), distinct from the named
        // ResourceInstanceSpec form covered by ValidReserveStatement.
        AssertRoundTripStable(TestPrograms.ValidReserveQuantityCategory);
    }

    [Fact]
    public void RoundTrip_ReserveWherePredicate_IsStable()
    {
        // where (Exp) clause combined with CategorySpecWithBinding ("1 Room r").
        // Exercises both the where-clause printer branch and the alias-carrying
        // resource-spec branch.
        AssertRoundTripStable(TestPrograms.ValidReserveWherePredicate);
    }

    [Fact]
    public void RoundTrip_RecurringStrictUntil_IsStable()
    {
        // Recurrence with STRICT mode and the "until DateTime" interval form.
        // Forces the printer's recurrence branch with endKw == "until".
        AssertRoundTripStable(TestPrograms.ValidRecurringStrictUntil);
    }

    [Fact]
    public void RoundTrip_RecurringFlexibleFor_IsStable()
    {
        // Recurrence with FLEXIBLE mode and the "for Duration" interval form.
        // Complementary to the STRICT/until case above, ensuring both
        // RecurrenceMode values and both RecurrenceInterval subtypes round-trip.
        AssertRoundTripStable(TestPrograms.ValidRecurringFlexibleFor);
    }

    [Fact]
    public void RoundTrip_IfStatement_IsStable()
    {
        // If statement with both then- and else-branches and a Bool condition
        // in scope. Exercises the printer's If branch and the parser's
        // mandatory braces around branch bodies.
        AssertRoundTripStable(TestPrograms.ValidIfStmt);
    }

    // Implements the parse → print → reparse → print → equal-check flow
    // described in the region header above. The numbered steps in the body
    // map one-to-one onto steps 1–5 in that comment.
    private static void AssertRoundTripStable(string source)
    {
        // 1. Parse the source string from TestPrograms.cs into the first AST.
        Stmt parsed1 = TestHelpers.ParseShouldSucceed(source);
        // 2. Pretty-print that AST into canonical RAL surface text.
        string text1 = AstPrettyPrinter.Print(parsed1);

        // 3. Re-parse the printed text. ParseShouldSucceed asserts errors.count
        //    == 0, so this step fails the test if the printer produced text
        //    the grammar cannot accept.
        Stmt parsed2 = TestHelpers.ParseShouldSucceed(text1);
        // 4. Pretty-print the second AST.
        string text2 = AstPrettyPrinter.Print(parsed2);

        // 5. The two printed texts must be byte-identical. If they differ,
        //    the front-end pipeline is not stable under round-trip.
        Assert.Equal(text1, text2);
    }
}
