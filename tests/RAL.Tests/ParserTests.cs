namespace RAL.Tests;

/*
 * Test level: unit (parser only — no type, AST-shape, or interpreter assertions).
 *
 * Scope:
 *   - The parser must reject obvious syntax errors.
 *   - The parser must accept the small number of grammar forms that no AST
 *     or TypeCheck test happens to exercise on the same source.
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
