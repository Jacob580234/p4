using RAL.AST;
using RAL.Interpreter;
using Interp = RAL.Interpreter.Interpreter;

namespace RAL.Tests;

/*
 * Tests for the Interpreter — both the expression evaluator (EvalExp) and
 * the statement executor (ExecStmt).
 *
 * Test levels: this class mixes unit and acceptance tests by design.
 *   - Expression-evaluator tests (top of file) construct AST nodes directly
 *     and assert on EvalExp output — unit tests of the interpreter.
 *   - Statement tests 1–7 also construct AST nodes directly and drive
 *     ExecStmt against a fresh environment — unit tests.
 *   - Statement tests 8–26 run the full Parse → TypeCheck → Interpret
 *     pipeline because they exercise side effects on ResourceRegistry /
 *     ReservationRegistry that only the Interpreter mutates — acceptance
 *     tests in the study-regulation sense (representative whole programs,
 *     observable behaviour).
 *
 * The Interpreter assumes the TypeChecker has already accepted the program,
 * so only well-typed inputs are used here (except the division-by-zero test).
 *
 * ResourceRegistry and ReservationRegistry are process-wide singletons.
 * This class implements IDisposable so the registries are cleared before
 * AND after every test (xUnit creates a fresh instance per [Fact]). That way
 * pipeline tests can reuse the same RAL source from TestPrograms.cs without
 * inventing unique category names to dodge cross-test pollution.
 */
public class InterpreterTests : IDisposable
{
    public InterpreterTests() => TestHelpers.ResetRegistries();
    public void Dispose()     => TestHelpers.ResetRegistries();

    // ── Literals ──────────────────────────────────────────────────────────────

    [Fact]
    public void NumberLiteral_EvaluatesToNumberVal()
    {
        Value result = TestHelpers.EvalExpression(new NumberV(1, 42f), new EnvV(), new EnvH());
        var nv = Assert.IsType<NumberVal>(result);
        Assert.Equal(42f, nv.Value);
    }

    [Fact]
    public void BoolLiteralTrue_EvaluatesToBoolValTrue()
    {
        Value result = TestHelpers.EvalExpression(new BoolV(1, true), new EnvV(), new EnvH());
        var bv = Assert.IsType<BoolVal>(result);
        Assert.True(bv.Value);
    }

    [Fact]
    public void BoolLiteralFalse_EvaluatesToBoolValFalse()
    {
        Value result = TestHelpers.EvalExpression(new BoolV(1, false),  new EnvV(), new EnvH());
        var bv = Assert.IsType<BoolVal>(result);
        Assert.False(bv.Value);
    }

    [Fact]
    public void StringLiteral_EvaluatesToStringVal()
    {
        Value result = TestHelpers.EvalExpression(new StringV(1, "hi"),  new EnvV(), new EnvH());
        var sv = Assert.IsType<StringVal>(result);
        Assert.Equal("hi", sv.Value);
    }

    // ── Arithmetic ────────────────────────────────────────────────────────────

    [Fact]
    public void Addition_TwoNumbers_ReturnsCorrectSum()
    {
        // 2 + 3 = 5
        var exp = new BinaryOperation(1, new NumberV(1, 2), BinaryOperator.ADD, new NumberV(1, 3));
        var result = Assert.IsType<NumberVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.Equal(5f, result.Value, precision: 4);
    }

    [Fact]
    public void Subtraction_TwoNumbers_ReturnsCorrectDifference()
    {
        // 10 - 4 = 6
        var exp = new BinaryOperation(1, new NumberV(1, 10), BinaryOperator.SUB, new NumberV(1, 4));
        var result = Assert.IsType<NumberVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.Equal(6f, result.Value, precision: 4);
    }

    [Fact]
    public void Multiplication_TwoNumbers_ReturnsCorrectProduct()
    {
        // 3 * 4 = 12
        var exp = new BinaryOperation(1, new NumberV(1, 3), BinaryOperator.MUL, new NumberV(1, 4));
        var result = Assert.IsType<NumberVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.Equal(12f, result.Value, precision: 4);
    }

    [Fact]
    public void Division_TwoNumbers_ReturnsCorrectQuotient()
    {
        // 10 / 4 = 2.5
        var exp = new BinaryOperation(1, new NumberV(1, 10), BinaryOperator.DIV, new NumberV(1, 4));
        var result = Assert.IsType<NumberVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.Equal(2.5f, result.Value, precision: 4);
    }

    [Fact]
    public void AddMulPrecedence_ViaDirectAst_EvaluatesTo14()
    {
        // Construct: 2 + (3 * 4) = 14
        // This is what the parser produces for "2 + 3 * 4" (MUL binds tighter).
        var mul = new BinaryOperation(1, new NumberV(1, 3), BinaryOperator.MUL, new NumberV(1, 4));
        var add = new BinaryOperation(1, new NumberV(1, 2), BinaryOperator.ADD, mul);
        var result = Assert.IsType<NumberVal>(TestHelpers.EvalExpression(add,  new EnvV(), new EnvH()));
        Assert.Equal(14f, result.Value, precision: 4);
    }

    // ── Comparison ────────────────────────────────────────────────────────────

    [Fact]
    public void Equality_TwoEqualNumbers_ReturnsTrue()
    {
        var exp = new BinaryOperation(1, new NumberV(1, 2), BinaryOperator.EQ, new NumberV(1, 2));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    [Fact]
    public void Equality_TwoDifferentNumbers_ReturnsFalse()
    {
        var exp = new BinaryOperation(1, new NumberV(1, 2), BinaryOperator.EQ, new NumberV(1, 3));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.False(result.Value);
    }

    [Fact]
    public void LessThan_LeftSmaller_ReturnsTrue()
    {
        var exp = new BinaryOperation(1, new NumberV(1, 1), BinaryOperator.LT, new NumberV(1, 5));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    [Fact]
    public void GreaterThan_LeftLarger_ReturnsTrue()
    {
        var exp = new BinaryOperation(1, new NumberV(1, 10), BinaryOperator.GT, new NumberV(1, 5));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    // ── Boolean operators ─────────────────────────────────────────────────────

    [Fact]
    public void Not_False_ReturnsTrue()
    {
        var exp = new UnaryOperation(1, UnaryOperator.NOT, new BoolV(1, false));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    [Fact]
    public void Not_True_ReturnsFalse()
    {
        var exp = new UnaryOperation(1, UnaryOperator.NOT, new BoolV(1, true));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.False(result.Value);
    }

    [Fact]
    public void And_TrueAndTrue_ReturnsTrue()
    {
        var exp = new BinaryOperation(1, new BoolV(1, true), BinaryOperator.AND, new BoolV(1, true));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    [Fact]
    public void And_TrueAndFalse_ReturnsFalse()
    {
        var exp = new BinaryOperation(1, new BoolV(1, true), BinaryOperator.AND, new BoolV(1, false));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.False(result.Value);
    }

    [Fact]
    public void Or_FalseOrTrue_ReturnsTrue()
    {
        var exp = new BinaryOperation(1, new BoolV(1, false), BinaryOperator.OR, new BoolV(1, true));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    [Fact]
    public void StringEquality_SameStrings_ReturnsTrue()
    {
        var exp = new BinaryOperation(1, new StringV(1, "hi"), BinaryOperator.EQ, new StringV(1, "hi"));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    [Fact]
    public void StringEquality_DifferentStrings_ReturnsFalse()
    {
        var exp = new BinaryOperation(1, new StringV(1, "hi"), BinaryOperator.EQ, new StringV(1, "bye"));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.False(result.Value);
    }

    // ── Unary negation ────────────────────────────────────────────────────────

    [Fact]
    public void UnaryNeg_OnNumber_EvaluatesToNegativeValue()
    {
        // UnaryOperation(NEG, NumberV(5)) should evaluate to NumberVal(-5).
        var exp = new UnaryOperation(1, UnaryOperator.NEG, new NumberV(1, 5));
        var result = Assert.IsType<NumberVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.Equal(-5f, result.Value, precision: 4);
    }

    [Fact]
    public void UnaryNeg_OnZero_EvaluatesToZero()
    {
        var exp = new UnaryOperation(1, UnaryOperator.NEG, new NumberV(1, 0));
        var result = Assert.IsType<NumberVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.Equal(0f, result.Value, precision: 4);
    }

    // ── Runtime errors ────────────────────────────────────────────────────────

    [Fact]
    public void DivisionByZero_ThrowsException()
    {
        var exp = new BinaryOperation(1, new NumberV(1, 5), BinaryOperator.DIV, new NumberV(1, 0));
        var ex = Assert.Throws<Exception>(() => TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.Contains("zero", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reference_UnboundName_ThrowsException()
    {
        // Evaluating Reference("x", null) against an empty EnvV must throw —
        // the lookup has no binding to return. Reference itself IS supported
        // (see Reference_DeclaredVariable_ReturnsBoundValue); only unbound lookup fails.
        var exp = new Reference(1, "x", null);
        Assert.Throws<Exception>(() => TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
    }

    [Fact]
    public void InvalidUnary_NotOnNumber_ThrowsException()
    {
        // NOT applied to a NumberV: wrong type at runtime.
        // The TypeChecker would reject this, but the Interpreter guards too.
        var exp = new UnaryOperation(1, UnaryOperator.NOT, new NumberV(1, 5));
        Assert.Throws<Exception>(() => TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
    }

    // ── DateTime / Duration: literals ────────────────────────────────────────

    [Fact]
    public void DateTimeLiteral_EvaluatesToDateTimeVal()
    {
        var dt = new DateTime(2026, 3, 15, 14, 0, 0);
        Value result = TestHelpers.EvalExpression(new DateTimeV(1, dt),  new EnvV(), new EnvH());
        var val = Assert.IsType<DateTimeVal>(result);
        Assert.Equal(dt, val.Value);
    }

    [Fact]
    public void DurationLiteral_EvaluatesToDurationVal()
    {
        var span = TimeSpan.FromDays(2);
        Value result = TestHelpers.EvalExpression(new DurationV(1, span),  new EnvV(), new EnvH());
        var val = Assert.IsType<DurationVal>(result);
        Assert.Equal(span, val.Value);
    }

    // ── DateTime / Duration: arithmetic ──────────────────────────────────────

    [Fact]
    public void DateTimePlusDuration_ReturnsShiftedDateTime()
    {
        // 15/03-2026 + 2 days = 17/03-2026
        var start = new DateTime(2026, 3, 15);
        var period = TimeSpan.FromDays(2);
        var exp = new BinaryOperation(1,
            new DateTimeV(1, start),
            BinaryOperator.ADD,
            new DurationV(1, period));
        var result = Assert.IsType<DateTimeVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.Equal(new DateTime(2026, 3, 17), result.Value);
    }

    [Fact]
    public void DateTimeMinusDuration_ReturnsShiftedDateTime()
    {
        // 17/03-2026 - 2 days = 15/03-2026
        var start = new DateTime(2026, 3, 17);
        var period = TimeSpan.FromDays(2);
        var exp = new BinaryOperation(1,
            new DateTimeV(1, start),
            BinaryOperator.SUB,
            new DurationV(1, period));
        var result = Assert.IsType<DateTimeVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.Equal(new DateTime(2026, 3, 15), result.Value);
    }

    // ── DateTime / Duration: equality ────────────────────────────────────────

    [Fact]
    public void DateTimeEquality_SameDateTimes_ReturnsTrue()
    {
        var dt = new DateTime(2026, 3, 15);
        var exp = new BinaryOperation(1,
            new DateTimeV(1, dt), BinaryOperator.EQ, new DateTimeV(1, dt));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    [Fact]
    public void DateTimeEquality_DifferentDateTimes_ReturnsFalse()
    {
        var exp = new BinaryOperation(1,
            new DateTimeV(1, new DateTime(2026, 3, 15)),
            BinaryOperator.EQ,
            new DateTimeV(1, new DateTime(2026, 3, 16)));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.False(result.Value);
    }

    [Fact]
    public void DurationEquality_SameDurations_ReturnsTrue()
    {
        var span = TimeSpan.FromHours(2);
        var exp = new BinaryOperation(1,
            new DurationV(1, span), BinaryOperator.EQ, new DurationV(1, span));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    [Fact]
    public void DurationEquality_DifferentDurations_ReturnsFalse()
    {
        var exp = new BinaryOperation(1,
            new DurationV(1, TimeSpan.FromHours(1)),
            BinaryOperator.EQ,
            new DurationV(1, TimeSpan.FromHours(2)));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.False(result.Value);
    }

    // ── DateTime / Duration: ordering ────────────────────────────────────────

    [Fact]
    public void DateTimeLessThan_EarlierIsSmaller_ReturnsTrue()
    {
        var exp = new BinaryOperation(1,
            new DateTimeV(1, new DateTime(2026, 3, 15)),
            BinaryOperator.LT,
            new DateTimeV(1, new DateTime(2026, 3, 16)));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    [Fact]
    public void DateTimeLessThan_LaterIsNotSmaller_ReturnsFalse()
    {
        var exp = new BinaryOperation(1,
            new DateTimeV(1, new DateTime(2026, 3, 16)),
            BinaryOperator.LT,
            new DateTimeV(1, new DateTime(2026, 3, 15)));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.False(result.Value);
    }

    [Fact]
    public void DurationLessThan_ShorterIsSmaller_ReturnsTrue()
    {
        var exp = new BinaryOperation(1,
            new DurationV(1, TimeSpan.FromHours(1)),
            BinaryOperator.LT,
            new DurationV(1, TimeSpan.FromHours(2)));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    [Fact]
    public void DurationGreaterThan_LongerIsLarger_ReturnsTrue()
    {
        var exp = new BinaryOperation(1,
            new DurationV(1, TimeSpan.FromHours(3)),
            BinaryOperator.GT,
            new DurationV(1, TimeSpan.FromHours(1)));
        var result = Assert.IsType<BoolVal>(TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
        Assert.True(result.Value);
    }

    // ── DateTime / Duration: invalid operations reach the runtime guard ───────

    [Fact]
    public void DateTimePlusDateTime_ThrowsAtRuntime()
    {
        // The typechecker rejects this; if it reaches the interpreter anyway, it must throw.
        var exp = new BinaryOperation(1,
            new DateTimeV(1, new DateTime(2026, 3, 15)),
            BinaryOperator.ADD,
            new DateTimeV(1, new DateTime(2026, 3, 16)));
        Assert.Throws<Exception>(() => TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
    }

    [Fact]
    public void DurationPlusDateTime_ThrowsAtRuntime()
    {
        var exp = new BinaryOperation(1,
            new DurationV(1, TimeSpan.FromDays(1)),
            BinaryOperator.ADD,
            new DateTimeV(1, new DateTime(2026, 3, 15)));
        Assert.Throws<Exception>(() => TestHelpers.EvalExpression(exp, new EnvV(), new EnvH()));
    }

    // ── Statement execution: unit (tests 1–7) → acceptance (tests 8–26) ─────
    //
    // These tests drive Interpreter.ExecStmt rather than EvalExp.
    // Tests 1–7 are unit-style: direct AST construction, no parser involved.
    // Tests 8–26 are acceptance-style: full parse → typecheck → interpret
    // pipeline on representative source programs that declare categories /
    // resources / reservations the interpreter mutates via side-effects on
    // EnvV and the global registries. The region break below marks the
    // unit → acceptance boundary inline. The acceptance tier itself splits
    // into three groups, separated by region headers further down:
    //   8–15  basic statement and expression coverage (decls, move, reserve,
    //         cancel, reschedule, availability) — one observable side effect
    //         per test.
    //   16–25 domain-semantics coverage — composite reservations, recurrence
    //         expansion, where-clause filtering at runtime, conflict
    //         detection, move propagation through availability, template
    //         parameter binding (single and compound booking patterns), and
    //         if-branch scope isolation.
    //   26    end-to-end scenario — a single program that exercises every
    //         front-end module cooperatively (category hierarchy, resource
    //         declarations with properties, move, two reserves, cancel) and
    //         asserts on the joint final state. Matches the study regulation's
    //         "run the compiler on a number of programs" acceptance criterion.
    //
    // Pipeline tests reuse RAL source from TestPrograms.cs. The class-level
    // ResetRegistries (constructor / Dispose) clears the ResourceRegistry and
    // ReservationRegistry singletons between tests, so the same canonical
    // names ("Room", "myRoom", "res") can appear in every test without
    // cross-test pollution.


    [Fact]  // 1
    public void VarDecl_Number_BindsZeroDefault()
    {
        // "Number x;" must bind x → NumberVal(0) in the current scope.
        var stmt = new VarDecl(1, new NumberT(), "x");
        var envV = new EnvV();
        Interp.ExecStmt(stmt, envV, new EnvH(), new EnvTem());
        var value = Assert.IsType<NumberVal>(envV.Lookup("x"));
        Assert.Equal(0f, value.Value, precision: 4);
    }

    [Fact]  // 2
    public void VarDecl_Bool_BindsFalseDefault()
    {
        // "Bool b;" must bind b → BoolVal(false).
        var stmt = new VarDecl(1, new BoolT(), "b");
        var envV = new EnvV();
        Interp.ExecStmt(stmt, envV, new EnvH(), new EnvTem());
        var value = Assert.IsType<BoolVal>(envV.Lookup("b"));
        Assert.False(value.Value);
    }

    [Fact]  // 3
    public void Composite_ExecutesLeftThenRight()
    {
        // Left declares x; right assigns 5 to x.
        // If the order were reversed, the Assignment would throw because
        // x would not yet be bound — so a successful x == 5 confirms left-then-right.
        var stmt = new Composite(1,
            new VarDecl(1, new NumberT(), "x"),
            new ExpStmt(1, new Assignment(1,
                new Reference(1, "x", null),
                new NumberV(1, 5))));
        var envV = new EnvV();
        Interp.ExecStmt(stmt, envV, new EnvH(), new EnvTem());
        var value = Assert.IsType<NumberVal>(envV.Lookup("x"));
        Assert.Equal(5f, value.Value, precision: 4);
    }

    [Fact]  // 4
    public void Reference_DeclaredVariable_ReturnsBoundValue()
    {
        // Pre-bind x → 7 in envV; evaluating Reference("x", null) must return that value.
        var envV = new EnvV();
        envV.Bind("x", new NumberVal(7));
        var value = Assert.IsType<NumberVal>(
            TestHelpers.EvalExpression(new Reference(1, "x", null), envV, new EnvH()));
        Assert.Equal(7f, value.Value, precision: 4);
    }

    [Fact]  // 5
    public void Assignment_VariableForm_UpdatesBinding()
    {
        // Pre-bind x → 0; evaluate Assignment(x, 9); envV.Lookup("x") must be 9.
        var envV = new EnvV();
        envV.Bind("x", new NumberVal(0));
        TestHelpers.EvalExpression(
            new Assignment(1, new Reference(1, "x", null), new NumberV(1, 9)),
            envV, new EnvH());
        var value = Assert.IsType<NumberVal>(envV.Lookup("x"));
        Assert.Equal(9f, value.Value, precision: 4);
    }

    [Fact]  // 6
    public void If_TrueCondition_ExecutesThenBranch()
    {
        // Pre-bind flag → 0; if (true) then { flag = 1; } else { flag = 2; }
        // After execution flag must be 1.
        var envV = new EnvV();
        envV.Bind("flag", new NumberVal(0));
        var ifStmt = new If(1,
            new BoolV(1, true),
            new ExpStmt(1, new Assignment(1, new Reference(1, "flag", null), new NumberV(1, 1))),
            new ExpStmt(1, new Assignment(1, new Reference(1, "flag", null), new NumberV(1, 2))));
        Interp.ExecStmt(ifStmt, envV, new EnvH(), new EnvTem());
        var value = Assert.IsType<NumberVal>(envV.Lookup("flag"));
        Assert.Equal(1f, value.Value, precision: 4);
    }

    [Fact]  // 7
    public void If_FalseCondition_ExecutesElseBranch()
    {
        // Pre-bind flag → 0; if (false) then { flag = 1; } else { flag = 2; }
        // After execution flag must be 2.
        var envV = new EnvV();
        envV.Bind("flag", new NumberVal(0));
        var ifStmt = new If(1,
            new BoolV(1, false),
            new ExpStmt(1, new Assignment(1, new Reference(1, "flag", null), new NumberV(1, 1))),
            new ExpStmt(1, new Assignment(1, new Reference(1, "flag", null), new NumberV(1, 2))));
        Interp.ExecStmt(ifStmt, envV, new EnvH(), new EnvTem());
        var value = Assert.IsType<NumberVal>(envV.Lookup("flag"));
        Assert.Equal(2f, value.Value, precision: 4);
    }

    // ── Acceptance: basic statement coverage (tests 8–15) ───────────────────
    //
    // Each program exercises one statement form and asserts on one observable
    // side effect. Availability tests (14–15) sit at the tail of this tier
    // because they share the "single statement, one observable outcome"
    // shape, even though the outcome is captured from Console.Out rather
    // than from an EnvV binding.

    [Fact]  // 8
    public void CategoryDecl_RegistersInResourceRegistry()
    {
        // After running "category Room;" the registry must contain a Room bucket.
        // ResourceRegistry.ToString contains the category name in its header per bucket.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidCategory);
        TestHelpers.RunTypeChecker(root);
        Interp.ExecStmt(root, new EnvV(), new EnvH(), new EnvTem());
        Assert.Contains("Room", ResourceRegistry.Instance().ToString());
    }

    [Fact]  // 9
    public void ResourceDecl_CreatesResourceValWithPropertyMap()
    {
        // "Room myRoom { Number beds = 2; }" — envV.Lookup("myRoom") must be a
        // ResourceVal whose Properties["beds"] equals NumberVal(2).
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidResourceWithProperty);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());
        var resource = Assert.IsType<ResourceVal>(envV.Lookup("myRoom"));
        Assert.Equal("Room", resource.CategoryId);
        var beds = Assert.IsType<NumberVal>(resource.Properties["beds"]);
        Assert.Equal(2f, beds.Value, precision: 4);
    }

    [Fact]  // 10
    public void Move_UpdatesRegistryBucketAndResourceCategoryId()
    {
        // After "move myRoom to Suite;" the resource's CategoryId must be "Suite"
        // and the registry must list it under Suite.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidMoveResourceToCategory);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());
        var resource = Assert.IsType<ResourceVal>(envV.Lookup("myRoom"));
        Assert.Equal("Suite", resource.CategoryId);
        Assert.Contains("Suite", ResourceRegistry.Instance().ToString());
    }

    [Fact]  // 11
    public void Reserve_NamedResourceInValidInterval_ProducesNonFailedReservation()
    {
        // "reserve myRoom from 15/03-2026 to 16/03-2026" binds res to a
        // ReservationVal whose Failed() is false (Reservations list non-empty).
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidReserveStatement);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());
        var reservation = Assert.IsType<ReservationVal>(envV.Lookup("res"));
        Assert.False(reservation.Failed());
    }

    [Fact]  // 12
    public void Cancel_RemovesReservationFromRegistry()
    {
        // After "cancel res;" the reservation variable must report Failed() == true
        // (i.e. its Reservations list is empty).
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidCancelReservation);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());
        var reservation = Assert.IsType<ReservationVal>(envV.Lookup("res"));
        Assert.True(reservation.Failed());
    }

    [Fact]  // 13
    public void Reschedule_ReturnsReservationWithNewInterval()
    {
        // "reschedule res from 20/03-2026 to 21/03-2026" returns a ReservationVal
        // whose atom carries the new start/end.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidRescheduleReservation);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());
        var reservation = Assert.IsType<ReservationVal>(envV.Lookup("rescheduled"));
        Assert.False(reservation.Failed());
        var atom = Assert.Single(reservation.Reservations);
        Assert.Equal(new DateTime(2026, 3, 20), atom.Start.Value);
        Assert.Equal(new DateTime(2026, 3, 21), atom.End.Value);
    }

    // HandleAvailability has no envV side effect; its result is the text it
    // prints to standard output. Tests 14 and 15 redirect Console.Out so they
    // can assert on that user-visible contract directly.

    [Fact]  // 14
    public void Availability_NoConflict_ReportsAvailable()
    {
        // A "check" against a freshly declared resource with no prior
        // reservations must print the success banner ("Availability check
        // succeeded ..."). Captures Console.Out for the duration of the
        // interpret pass and restores it in finally so the redirect cannot
        // leak into adjacent tests.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidAvailabilityQuery);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();

        var captured = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(captured);
        try
        {
            Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Contains("Availability check succeeded", captured.ToString());
    }

    [Fact]  // 15
    public void Availability_WithConflictingReservation_ReportsUnavailable()
    {
        // After reserving myRoom for 15/03 → 17/03, a check for 16/03 → 18/03
        // overlaps the existing booking; ReservationRegistry.IsAvailable
        // returns false for myRoom in that interval, so HandleAvailability
        // must print the failure banner ("Availability check failed ...").
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.AvailabilityWithConflictProgram);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();

        var captured = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(captured);
        try
        {
            Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = captured.ToString();
        Assert.Contains("Availability check failed", output);
        Assert.DoesNotContain("Availability check succeeded", output);
    }

    // ── Acceptance: domain-semantics coverage (tests 16–25) ─────────────────
    //
    // Acceptance-level tests that drive the full Parse → TypeCheck → Interpret
    // pipeline. Each one pins a piece of domain semantics that the simpler
    // single-statement tests above do not cover. The three composite-reservation
    // tests (16–18) sit together so that "seq", "and" and "or" are read as one
    // group.

    [Fact]  // 16
    public void CompositeSeq_TwoReserves_ProducesCompositeReservationWithBothAtoms()
    {
        // "reserve A seq reserve B" combines two atomic reservations into one
        // composite ReservationVal. Both reserves target myRoom on different,
        // non-overlapping intervals, so both succeed and the result must hold
        // exactly two atoms in source order.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.CompositeSeqReservationProgram);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());

        var reservation = Assert.IsType<ReservationVal>(envV.Lookup("res"));
        Assert.False(reservation.Failed());
        Assert.True(reservation.IsComposite());
        Assert.Equal(2, reservation.Reservations.Count);

        // Atoms come out in source order: left operand first, then right.
        Assert.Equal(new DateTime(2026, 3, 15), reservation.Reservations[0].Start.Value);
        Assert.Equal(new DateTime(2026, 3, 16), reservation.Reservations[0].End.Value);
        Assert.Equal(new DateTime(2026, 3, 17), reservation.Reservations[1].Start.Value);
        Assert.Equal(new DateTime(2026, 3, 18), reservation.Reservations[1].End.Value);
    }

    [Fact]  // 17
    public void CompositeAnd_TwoReserves_ProducesCompositeReservationWithBothAtoms()
    {
        // "reserve A and reserve B" requires both sides to succeed. Both
        // reserves target myRoom on disjoint intervals (15/03 → 16/03 and
        // 17/03 → 18/03), so EvalReserveAND merges the right atom into the
        // left and the result holds two atoms in source order.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidReserveAndReserve);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());

        var reservation = Assert.IsType<ReservationVal>(envV.Lookup("res"));
        Assert.False(reservation.Failed());
        Assert.True(reservation.IsComposite());
        Assert.Equal(2, reservation.Reservations.Count);

        Assert.Equal(new DateTime(2026, 3, 15), reservation.Reservations[0].Start.Value);
        Assert.Equal(new DateTime(2026, 3, 16), reservation.Reservations[0].End.Value);
        Assert.Equal(new DateTime(2026, 3, 17), reservation.Reservations[1].Start.Value);
        Assert.Equal(new DateTime(2026, 3, 18), reservation.Reservations[1].End.Value);
    }

    [Fact]  // 18
    public void CompositeOr_LeftSucceeds_ShortCircuitsRight()
    {
        // "reserve A or reserve B" short-circuits: EvalReserveOR only attempts
        // the right operand when the left one fails. Here the left reserve
        // (15/03 → 16/03) succeeds against an empty registry, so the right
        // is never evaluated and the result contains exactly the left atom.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidReserveOrReserve);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());

        var reservation = Assert.IsType<ReservationVal>(envV.Lookup("res"));
        Assert.False(reservation.Failed());
        Assert.False(reservation.IsComposite());

        var atom = Assert.Single(reservation.Reservations);
        Assert.Equal(new DateTime(2026, 3, 15), atom.Start.Value);
        Assert.Equal(new DateTime(2026, 3, 16), atom.End.Value);
    }

    [Fact]  // 19
    public void Recurring_StrictForThreeWeeks_ExpandsToFourAtomsAtWeeklyOffsets()
    {
        // "every 1 week for 3 weeks" anchored at 15/03 must expand into the
        // base atom plus three shifted atoms: 15/03, 22/03, 29/03, 5/04.
        // Each atom is one day long (end = start + 1 day, mirroring the base
        // interval 15/03 → 16/03). STRICT means EvalReserveAND chains them;
        // because the same resource at distinct non-overlapping times never
        // conflicts, every atom succeeds.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.RecurringWeeklyForThreeWeeksProgram);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());

        var reservation = Assert.IsType<ReservationVal>(envV.Lookup("res"));
        Assert.False(reservation.Failed());
        Assert.Equal(4, reservation.Reservations.Count);

        var expectedStarts = new[] {
            new DateTime(2026, 3, 15),
            new DateTime(2026, 3, 22),
            new DateTime(2026, 3, 29),
            new DateTime(2026, 4, 5),
        };
        for (int i = 0; i < expectedStarts.Length; i++)
        {
            Assert.Equal(expectedStarts[i], reservation.Reservations[i].Start.Value);
            Assert.Equal(expectedStarts[i].AddDays(1), reservation.Reservations[i].End.Value);
        }
    }

    [Fact]  // 20
    public void WhereClause_OnlyMatchingResourceIsSelectedAtRuntime()
    {
        // Two rooms declared, only roomTwoBeds satisfies r.beds == 2.
        // QueryEvaluator must filter the Cartesian product so that roomFourBeds
        // never appears in the selected combination. Assert: exactly one atom,
        // containing exactly one resource, and that resource is roomTwoBeds.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.WhereClauseFilteringProgram);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());

        var reservation = Assert.IsType<ReservationVal>(envV.Lookup("res"));
        Assert.False(reservation.Failed());
        var atom = Assert.Single(reservation.Reservations);
        var resource = Assert.Single(atom.Resources);
        Assert.Equal("roomTwoBeds", resource.ResourceId);
    }

    [Fact]  // 21
    public void ConflictingReservation_SecondAttemptOnSameInterval_Fails()
    {
        // First reserve takes myRoom for 15/03 → 16/03 and succeeds. The
        // second reserve targets the same room on the same interval, so
        // ReservationRegistry.IsAvailable returns false and EvalReserveAtom
        // returns an empty ReservationVal. "first" stays bound to a live
        // reservation; "second" must report Failed() == true.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ConflictingReservationProgram);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());

        var first = Assert.IsType<ReservationVal>(envV.Lookup("first"));
        Assert.False(first.Failed());

        var second = Assert.IsType<ReservationVal>(envV.Lookup("second"));
        Assert.True(second.Failed());
    }

    [Fact]  // 22
    public void MoveThenReserveInNewCategory_FindsMovedResource()
    {
        // Move myRoom from Room into Suite, then reserve "1 Suite". The
        // QueryEvaluator walks the Suite subtree, which now contains the
        // moved resource. The resulting atom must reserve exactly myRoom —
        // proving the move propagated through both the ResourceVal's
        // CategoryId and the ResourceRegistry's bucket layout.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.MoveThenReserveInNewCategoryProgram);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());

        var reservation = Assert.IsType<ReservationVal>(envV.Lookup("res"));
        Assert.False(reservation.Failed());
        var atom = Assert.Single(reservation.Reservations);
        var reserved = Assert.Single(atom.Resources);
        Assert.Equal("myRoom", reserved.ResourceId);
        Assert.Equal("Suite", reserved.CategoryId);
    }

    [Fact]  // 23
    public void TemplateCall_ExecutesReservationBody_AndUsesBoundParameter()
    {
        // bookStay(r) reserves the bound resource for 15/03 → 17/03. The
        // template is invoked with roomA, so the reservation produced inside
        // the body must reference exactly roomA on that interval. Verifies
        // the intended domain use case: templates as reusable booking
        // patterns whose parameter flows into a reserve statement and yields
        // an observable reservation side effect — not just a primitive
        // assignment to an outer variable.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.TemplateReservationBodyProgram);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());

        var reservation = Assert.IsType<ReservationVal>(envV.Lookup("stay"));
        Assert.False(reservation.Failed());
        var atom = Assert.Single(reservation.Reservations);
        var reserved = Assert.Single(atom.Resources);
        Assert.Equal("roomA", reserved.ResourceId);
        Assert.Equal(new DateTime(2026, 3, 15), atom.Start.Value);
        Assert.Equal(new DateTime(2026, 3, 17), atom.End.Value);
    }

    [Fact]  // 24
    public void TemplateCall_CompoundBookingPattern_ProducesStayAndCleaningReservations()
    {
        // bookStayWithClean(r) reserves the bound room for a
        // fixed stay window AND the global cleaner for a two-hour cleaning
        // slot immediately following the stay. After "use bookStayWithClean
        // (roomA);" the outer "booking" must hold a composite ReservationVal
        // whose first atom reserves roomA on 15/03 → 17/03 and whose second
        // atom reserves janitor on 17/03 00:00 → 17/03 02:00. The cleaning
        // atom's Start must equal the stay atom's End.
        // Complements test 23 (parameter-binding mechanic) with the actual
        // multi-statement composition that justifies templates as a DSL feature.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.TemplateCompoundBookingProgram);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());

        var reservation = Assert.IsType<ReservationVal>(envV.Lookup("booking"));
        Assert.False(reservation.Failed());
        Assert.True(reservation.IsComposite());
        Assert.Equal(2, reservation.Reservations.Count);

        // First atom: the stay — roomA, 15/03 → 17/03.
        var stayAtom = reservation.Reservations[0];
        var stayResource = Assert.Single(stayAtom.Resources);
        Assert.Equal("roomA", stayResource.ResourceId);
        Assert.Equal(new DateTime(2026, 3, 15), stayAtom.Start.Value);
        Assert.Equal(new DateTime(2026, 3, 17), stayAtom.End.Value);

        // Second atom: the cleaning slot — janitor, 17/03 00:00 → 17/03 02:00.
        var cleanAtom = reservation.Reservations[1];
        var cleanResource = Assert.Single(cleanAtom.Resources);
        Assert.Equal("janitor", cleanResource.ResourceId);
        Assert.Equal(new DateTime(2026, 3, 17, 0, 0, 0), cleanAtom.Start.Value);
        Assert.Equal(new DateTime(2026, 3, 17, 2, 0, 0), cleanAtom.End.Value);

        // "Immediately following the stay" — cleaning starts exactly when stay ends.
        Assert.Equal(stayAtom.End.Value, cleanAtom.Start.Value);
    }

    [Fact]  // 25
    public void IfThenBranch_LocalDeclaration_DoesNotLeakToOuterScope()
    {
        // "if (true) then { Number x = 5; }" — HandleIf opens a new scope for
        // the branch body, so VarDecl binds x only inside that child scope.
        // After execution the outer envV must NOT see x, and EnvV.Lookup must
        // throw the "Unknown name" exception its contract specifies.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.IfScopeIsolationProgram);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());

        Assert.Throws<Exception>(() => envV.Lookup("x"));
    }

    // ── End-to-end acceptance scenario (test 26) ────────────────────────────
    //
    // Multi-feature program: every front-end module cooperates in a single
    // run. The assertions form a joint contract on the final state, so a
    // regression in any one module (move, hierarchy resolution, where-clause
    // filtering, cancel side effects) surfaces here. See the program comment
    // on EndToEndScenarioProgram in TestPrograms.cs for the source listing
    // and the behavioural rationale behind each assertion.

    [Fact]  // 26
    public void EndToEndScenario_FrontEndModulesCooperate_OnFinalState()
    {
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.EndToEndScenarioProgram);
        TestHelpers.RunTypeChecker(root);
        var envV = new EnvV();
        Interp.ExecStmt(root, envV, new EnvH(), new EnvTem());

        // r1 was cancelled → its Reservations list must be empty.
        var r1 = Assert.IsType<ReservationVal>(envV.Lookup("r1"));
        Assert.True(r1.Failed());

        // r2 succeeded and reserved exactly one resource — the moved roomB,
        // selected via the where predicate r.beds == 4 over Room's subtree.
        var r2 = Assert.IsType<ReservationVal>(envV.Lookup("r2"));
        Assert.False(r2.Failed());
        var atom = Assert.Single(r2.Reservations);
        var reserved = Assert.Single(atom.Resources);
        Assert.Equal("roomB", reserved.ResourceId);
        Assert.Equal(new DateTime(2026, 3, 17), atom.Start.Value);
        Assert.Equal(new DateTime(2026, 3, 18), atom.End.Value);

        // Move propagated to the ResourceVal's CategoryId field; roomA was
        // never moved, so its category must still be Room.
        var roomA = Assert.IsType<ResourceVal>(envV.Lookup("roomA"));
        var roomB = Assert.IsType<ResourceVal>(envV.Lookup("roomB"));
        Assert.Equal("Room", roomA.CategoryId);
        Assert.Equal("Suite", roomB.CategoryId);

        // Property declarations survived parse, typecheck and interpret.
        Assert.Equal(2f, Assert.IsType<NumberVal>(roomA.Properties["beds"]).Value, precision: 4);
        Assert.Equal(4f, Assert.IsType<NumberVal>(roomB.Properties["beds"]).Value, precision: 4);
    }
}
