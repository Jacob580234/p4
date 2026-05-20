using RAL.AST;

namespace RAL.Tests;

/*
 * Test level: integration (parser + AST builder). Per Thomsen, the boundary
 * between unit and integration is fluid for compilers; asserting on parser
 * output exercises two phases together, which is the realistic input space
 * the downstream phases ever see.
 *
 * AST-shape tests for the parser's output.
 *
 * Tests assert on specific fields of named AST nodes (operators, identifiers,
 * sub-expression types) rather than on counts or boolean predicates, so a
 * failure points at the exact node that drifted.
 *
 * AstVisitor below provides traversal helpers for nodes that sit deep inside
 * the Composite tree (Reserve nested in Assignment in ExpStmt, If/Cancel/
 * TemplateCall whose position depends on the parser's Composite folding).
 * Tests reach top-level or shallow nodes directly via Composite navigation.
 */
file static class AstVisitor
{
    // Returns the where-clause Condition expression from the first Reserve or
    // Availability query in the tree, or null if absent.
    //
    // A Reserve sits deep in the expression tree, not the statement tree.
    // For a program ending in
    //   "Reservation res = reserve 1 Room r ... where (r.beds == 2);"
    // the path from the root looks like:
    //   Composite → Composite → Composite → ExpStmt
    //                                       └─ Assignment.Expression
    //                                          └─ Reserve.Query.Condition
    // Direct navigation would need six casts and would shift whenever the
    // surrounding program gains or loses a leading statement. This helper
    // recurses by node type, so its result is position-independent.
    public static Exp? FindFirstQueryCondition(Stmt root)
        => SearchForQueryCondition(root);

    private static Exp? SearchForQueryCondition(Stmt? stmt)
    {
        if (stmt is null) return null;
        return stmt switch
        {
            Availability av => av.Query.Condition,
            Composite c     => SearchForQueryCondition(c.Stmt1) ?? SearchForQueryCondition(c.Stmt2),
            ExpStmt es      => FindQueryConditionInExp(es.Expression),
            TemplateDecl td => SearchForQueryCondition(td.TemplateBody),
            _               => null
        };
    }

    private static Exp? FindQueryConditionInExp(Exp? exp)
    {
        if (exp is null) return null;
        return exp switch
        {
            Reserve r    => r.Query.Condition,
            Assignment a => FindQueryConditionInExp(a.Expression),
            _            => null
        };
    }

    // Returns the first If node in the statement tree, or null.
    public static If? FindFirstIf(Stmt root)
        => SearchForIf(root);

    private static If? SearchForIf(Stmt? stmt)
    {
        if (stmt is null) return null;
        return stmt switch
        {
            If ifNode   => ifNode,
            Composite c => SearchForIf(c.Stmt1) ?? SearchForIf(c.Stmt2),
            _           => null
        };
    }

    // Returns the first Cancel node in the statement tree, or null.
    public static Cancel? FindFirstCancel(Stmt root)
        => SearchForCancel(root);

    private static Cancel? SearchForCancel(Stmt? stmt)
    {
        if (stmt is null) return null;
        return stmt switch
        {
            Cancel cn   => cn,
            Composite c => SearchForCancel(c.Stmt1) ?? SearchForCancel(c.Stmt2),
            _           => null
        };
    }

    // Returns the first TemplateCall node in the statement tree, or null.
    // Needed because TemplateCall sits inside a Composite alongside TemplateDecl
    // and direct navigation would depend on the exact list position.
    public static TemplateCall? FindFirstTemplateCall(Stmt root)
        => SearchForTemplateCall(root);

    private static TemplateCall? SearchForTemplateCall(Stmt? stmt)
    {
        if (stmt is null) return null;
        return stmt switch
        {
            TemplateCall tc => tc,
            Composite c     => SearchForTemplateCall(c.Stmt1) ?? SearchForTemplateCall(c.Stmt2),
            TemplateDecl td => SearchForTemplateCall(td.TemplateBody),
            _               => null
        };
    }
}

// ── Tests ────────────────────────────────────────────────────────────────────

public class AstTraversalTests
{
    // ── Operator-precedence structure ─────────────────────────────────────────

    [Fact]
    public void AddMul_Precedence_AddIsAboveMul()
    {
        // "Number x = 2 + 3 * 4;" should produce:
        //   BinaryOperation(ADD, NumberV(2), BinaryOperation(MUL, NumberV(3), NumberV(4)))
        // ADD is the outer node because it has lower binding power.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidNestedArithmetic);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);

        var add = Assert.IsType<BinaryOperation>(rhs);
        Assert.Equal(BinaryOperator.ADD, add.Operator);

        var mul = Assert.IsType<BinaryOperation>(add.RightExpression);
        Assert.Equal(BinaryOperator.MUL, mul.Operator);
    }

    [Fact]
    public void ParenthesesOverride_MulIsAboveAdd()
    {
        // "Number x = (2 + 3) * 4;" should produce:
        //   BinaryOperation(MUL, BinaryOperation(ADD, NumberV(2), NumberV(3)), NumberV(4))
        // Parentheses force ADD to be the inner node.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidParenArithmetic);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);

        var mul = Assert.IsType<BinaryOperation>(rhs);
        Assert.Equal(BinaryOperator.MUL, mul.Operator);

        var add = Assert.IsType<BinaryOperation>(mul.LeftExpression);
        Assert.Equal(BinaryOperator.ADD, add.Operator);
    }

    // ── Literal expressions and unary operators ───────────────────────────────

    [Fact]
    public void BoolExpr_ContainsOneAndBinaryOperation()
    {
        // "Bool b = true and false;"
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidBoolExpr);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);
        var and = Assert.IsType<BinaryOperation>(rhs);
        Assert.Equal(BinaryOperator.AND, and.Operator);
    }

    [Fact]
    public void NotKeyword_ShouldProduceUnaryOperationNode()
    {
        // "Bool result = not(false);" must produce UnaryOperation(NOT, BoolV(false)).
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.NotFalse);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);

        var unary = Assert.IsType<UnaryOperation>(rhs);
        Assert.Equal(UnaryOperator.NOT, unary.Operator);
        Assert.IsType<BoolV>(unary.Expression);
        Assert.False(((BoolV)unary.Expression).Value);
    }

    [Fact]
    public void UnaryMinus_ShouldProduceNegOperationNode()
    {
        // "Number x = -5;" must produce UnaryOperation(NEG, NumberV(5)).
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.UnaryMinusFive);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);

        var unary = Assert.IsType<UnaryOperation>(rhs);
        Assert.Equal(UnaryOperator.NEG, unary.Operator);
        Assert.IsType<NumberV>(unary.Expression);
    }

    // ── Program structure: declarations ───────────────────────────────────────
    //
    // toComposite builds right-leaning trees: toComposite([A,B,C]) = Composite(A, Composite(B, C)).
    // A single-statement program's root IS the statement node with no wrapping Composite.

    [Fact]
    public void CategoryDecl_HasCorrectIdAndNullParent()
    {
        // "category Room;" — single statement; root IS the CategoryDecl node directly.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidCategory);
        var catDecl = Assert.IsType<CategoryDecl>(root);
        Assert.Equal("Room", catDecl.CategoryId);
        Assert.Null(catDecl.ParentId);  // no "is a" relation declared
    }

    [Fact]
    public void ResourceDecl_HasCorrectIdentifierAndCategoryType()
    {
        // "category Room;\nRoom myRoom {}"
        // Tree: Composite(CategoryDecl("Room"), ResourceDecl(ResourceT("Room"), "myRoom", null))
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidResourceDecl);
        var comp = Assert.IsType<Composite>(root);
        var resourceDecl = Assert.IsType<ResourceDecl>(comp.Stmt2);
        Assert.Equal("myRoom", resourceDecl.Identifier);
        var resourceType = Assert.IsType<ResourceT>(resourceDecl.Type);
        Assert.Equal("Room", resourceType.Category);
        Assert.Empty(resourceDecl.PropertyList);  // empty body {}
    }

    [Fact]
    public void IfStatement_HasReferenceConditionAndBodies()
    {
        // "Bool cond = true; if (cond) then {...} else {...}"
        // The If node is in the Composite tree — depth depends on the parser's
        // Composite folding, so we navigate with FindFirstIf rather than by position.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidIfStmt);
        If? ifStmt = AstVisitor.FindFirstIf(root);
        Assert.NotNull(ifStmt);
        var condRef = Assert.IsType<Reference>(ifStmt!.Condition);
        Assert.Equal("cond", condRef.VariableId);
        Assert.Null(condRef.PropertyId);
        Assert.NotNull(ifStmt.ThenBody);
        Assert.NotNull(ifStmt.ElseBody);
    }

    [Fact]
    public void TemplateDecl_HasCorrectIdAndParameters()
    {
        // "template booking(Number qty, String label) { ... }"
        // Tree: Composite(CategoryDecl, Composite(ResourceDecl, TemplateDecl))
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidResourceTemplate);
        var c1 = Assert.IsType<Composite>(root);
        var c2 = Assert.IsType<Composite>(c1.Stmt2);
        var tmplDecl = Assert.IsType<TemplateDecl>(c2.Stmt2);
        Assert.Equal("booking", tmplDecl.TemplateId);
        Assert.NotNull(tmplDecl.ParamList);
        Assert.Equal(2, tmplDecl.ParamList!.Count);
        Assert.Equal("qty",   tmplDecl.ParamList[0].Identifier);
        Assert.IsType<NumberT>(tmplDecl.ParamList[0].Type);
        Assert.Equal("label", tmplDecl.ParamList[1].Identifier);
        Assert.IsType<StringT>(tmplDecl.ParamList[1].Type);
    }

    // ── Core RAL features ─────────────────────────────────────────────────────

    [Fact]
    public void ReserveExpression_HasNamedResourceAndDateInterval()
    {
        // "reserve myRoom from 15/03-2026 to 16/03-2026"
        // Reserve is the RHS of the assignment. ResourceSpec carries the named resource.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidReserveStatement);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);
        var reserve = Assert.IsType<Reserve>(rhs);
        Assert.Single(reserve.Query.ResourceSpecs);
        // Named-resource form parses to ResourceInstanceSpec (not the category subtypes).
        var spec = Assert.IsType<ResourceInstanceSpec>(reserve.Query.ResourceSpecs[0]);
        Assert.Equal("myRoom", spec.ResourceId);
        Assert.IsType<DateTimeV>(reserve.Query.Interval.Start);
        Assert.IsType<DateTimeV>(reserve.Query.Interval.EndMarker);
    }

    [Fact]
    public void AvailabilityCheck_HasNamedResourceAndDateInterval()
    {
        // "check myRoom from 15/03-2026 to 16/03-2026"
        // Tree: Composite(CategoryDecl, Composite(ResourceDecl, Availability))
        // No where clause: Condition must be null.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidAvailabilityQuery);
        var c1 = Assert.IsType<Composite>(root);
        var c2 = Assert.IsType<Composite>(c1.Stmt2);
        var avail = Assert.IsType<Availability>(c2.Stmt2);
        Assert.Single(avail.Query.ResourceSpecs);
        var spec = Assert.IsType<ResourceInstanceSpec>(avail.Query.ResourceSpecs[0]);
        Assert.Equal("myRoom", spec.ResourceId);
        Assert.IsType<DateTimeV>(avail.Query.Interval.Start);
        Assert.IsType<DateTimeV>(avail.Query.Interval.EndMarker);
        Assert.Null(avail.Query.Condition);
    }

    [Fact]
    public void MoveStatement_HasCorrectResourceAndTargetCategory()
    {
        // "move myRoom to Suite;"
        // Tree: Composite(Cat, Composite(Cat, Composite(ResourceDecl, Move)))
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidMoveResourceToCategory);
        var c1 = Assert.IsType<Composite>(root);
        var c2 = Assert.IsType<Composite>(c1.Stmt2);
        var c3 = Assert.IsType<Composite>(c2.Stmt2);
        var move = Assert.IsType<Move>(c3.Stmt2);
        Assert.Equal("myRoom", move.ResourceId);
        Assert.Equal(new ResourceT("Suite"),  move.Type);
    }

    [Fact]
    public void CancelStatement_HasReservationVariableReference()
    {
        // "cancel res;" — Cancel.Reservation must be Reference("res", null).
        // FindFirstCancel navigates the Composite tree to avoid position-dependency.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidCancelReservation);
        Cancel? cancel = AstVisitor.FindFirstCancel(root);
        Assert.NotNull(cancel);
        var cancelRef = Assert.IsType<Reference>(cancel!.Reservation);
        Assert.Equal("res", cancelRef.VariableId);
        Assert.Null(cancelRef.PropertyId);
    }

    [Fact]
    public void RescheduleExpression_HasReservationReferenceAndNewInterval()
    {
        // "reschedule res from 20/03-2026 to 21/03-2026" is the second assignment's RHS.
        // Reschedule carries: the reservation it acts on, and the new TimeSpec.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidRescheduleReservation);
        Exp rhs = TestHelpers.ExtractNthAssignmentRhs(root, 2);
        var reschedule = Assert.IsType<Reschedule>(rhs);
        var reservationRef = Assert.IsType<Reference>(reschedule.Reservation);
        Assert.Equal("res", reservationRef.VariableId);
        Assert.IsType<DateTimeV>(reschedule.NewTimeInterval.Start);
        Assert.IsType<DateTimeV>(reschedule.NewTimeInterval.EndMarker);
    }

    [Fact]
    public void PropertyAccess_HasCorrectVariableIdAndPropertyId()
    {
        // "Number numBeds = myRoom.beds;" — RHS is Reference("myRoom", "beds").
        // ExtractFirstAssignmentRhs skips ResourceDecl.PropertyList, so the
        // first assignment it finds is the numBeds = myRoom.beds one.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidResourcePropertyAccess);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);
        var propertyRef = Assert.IsType<Reference>(rhs);
        Assert.Equal("myRoom", propertyRef.VariableId);
        Assert.Equal("beds",   propertyRef.PropertyId);
    }

    [Fact]
    public void ResourceDeclWithProperty_HasSingleFieldWithCorrectNameAndType()
    {
        // "Room myRoom { Number beds = 2; }"
        // PropertyList has one entry: Composite(VarDecl(NumberT, "beds"), ExpStmt(Assignment)).
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidResourceWithProperty);
        var comp = Assert.IsType<Composite>(root);
        var resourceDecl = Assert.IsType<ResourceDecl>(comp.Stmt2);
        Assert.Equal("myRoom", resourceDecl.Identifier);
        var resourceType = Assert.IsType<ResourceT>(resourceDecl.Type);
        Assert.Equal("Room", resourceType.Category);
        Assert.NotNull(resourceDecl.PropertyList);
        Assert.Single(resourceDecl.PropertyList!);
        var propComposite = Assert.IsType<Composite>(resourceDecl.PropertyList![0]);
        var fieldDecl = Assert.IsType<VarDecl>(propComposite.Stmt1);
        Assert.Equal("beds", fieldDecl.Identifier);
        Assert.IsType<NumberT>(fieldDecl.Type);
    }

    // ── DateTime / Duration: AST shape ───────────────────────────────────────

    [Fact]
    public void DateTimeLiteral_ParsesAsDateTimeVWithCorrectDate()
    {
        // "DateTime dt = 15/03-2026;" → RHS must be DateTimeV carrying March 15, 2026.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidDateTimeDeclaration);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);
        var dt = Assert.IsType<DateTimeV>(rhs);
        Assert.Equal(new DateTime(2026, 3, 15), dt.Value.Date);
    }

    [Fact]
    public void DateTimeLiteralWithTime_ParsesAsDateTimeV()
    {
        // "DateTime dt = 15/03-2026 14:00;" — optional time component must be accepted.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidDateTimeWithTime);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);
        var dt = Assert.IsType<DateTimeV>(rhs);
        Assert.Equal(14, dt.Value.Hour);
        Assert.Equal(0,  dt.Value.Minute);
    }

    [Fact]
    public void DurationLiteral_ParsesAsDurationVWithCorrectTimeSpan()
    {
        // "Duration dur = 2 days;" → RHS must be DurationV with TimeSpan of exactly 2 days.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidDurationDeclaration);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);
        var dur = Assert.IsType<DurationV>(rhs);
        Assert.Equal(TimeSpan.FromDays(2), dur.Value);
    }

    [Fact]
    public void DateTimePlusDurationInline_IsBinaryAddWithDateTimeVAndDurationV()
    {
        // "DateTime dt = 15/03-2026 + 2 days;" — RHS must be ADD(DateTimeV, DurationV).
        Stmt root = TestHelpers.ParseShouldSucceed("DateTime dt = 15/03-2026 + 2 days;");
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);
        var add = Assert.IsType<BinaryOperation>(rhs);
        Assert.Equal(BinaryOperator.ADD, add.Operator);
        Assert.IsType<DateTimeV>(add.LeftExpression);
        Assert.IsType<DurationV>(add.RightExpression);
    }

    [Fact]
    public void DateTimeComparison_IsBinaryLtWithTwoDateTimeV()
    {
        // "Bool result = 15/03-2026 < 16/03-2026;" — must be LT(DateTimeV, DateTimeV).
        Stmt root = TestHelpers.ParseShouldSucceed("Bool result = 15/03-2026 < 16/03-2026;");
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);
        var lt = Assert.IsType<BinaryOperation>(rhs);
        Assert.Equal(BinaryOperator.LT, lt.Operator);
        Assert.IsType<DateTimeV>(lt.LeftExpression);
        Assert.IsType<DateTimeV>(lt.RightExpression);
    }

    // ── Where clauses: AST shape ──────────────────────────────────────────────

    [Fact]
    public void ReserveWhereClause_ConditionHasPropertyReference()
    {
        // "where (r.beds == 2)" — condition must be EQ(Reference("r","beds"), NumberV).
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidReserveWherePredicate);
        Exp? condition = AstVisitor.FindFirstQueryCondition(root);
        Assert.NotNull(condition);
        var eq = Assert.IsType<BinaryOperation>(condition!);
        Assert.Equal(BinaryOperator.EQ, eq.Operator);
        var propertyRef = Assert.IsType<Reference>(eq.LeftExpression);
        Assert.Equal("r",    propertyRef.VariableId);
        Assert.Equal("beds", propertyRef.PropertyId);
    }

    // ── Template calls: AST shape ──────────────────────────────────────────────

    [Fact]
    public void TemplateCall_HasCorrectIdAndTypedArguments()
    {
        // "use booking(2, \"meeting\");" must produce:
        //   TemplateCall("booking", [NumberV(2), StringV("meeting")])
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidTemplateCall);
        TemplateCall? tc = AstVisitor.FindFirstTemplateCall(root);
        Assert.NotNull(tc);
        Assert.Equal("booking", tc!.TemplateId);
        Assert.NotNull(tc.ArgList);
        Assert.Equal(2, tc.ArgList!.Count);
        Assert.IsType<NumberV>(tc.ArgList[0]);
        Assert.IsType<StringV>(tc.ArgList[1]);
    }

    // ── Boolean operator precedence: AND binds tighter than OR ───────────────

    [Fact]
    public void AndOr_Precedence_AndIsAboveOr()
    {
        // "Bool result = true and false or true;" must produce:
        //   BinaryOperation(OR,
        //     BinaryOperation(AND, BoolV(true), BoolV(false)),
        //     BoolV(true))
        // OR is the outer node because it has lower binding power than AND.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidAndOrPrecedence);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);

        var or = Assert.IsType<BinaryOperation>(rhs);
        Assert.Equal(BinaryOperator.OR, or.Operator);

        var and = Assert.IsType<BinaryOperation>(or.LeftExpression);
        Assert.Equal(BinaryOperator.AND, and.Operator);
        Assert.IsType<BoolV>(and.LeftExpression);
        Assert.IsType<BoolV>(and.RightExpression);

        Assert.IsType<BoolV>(or.RightExpression);
    }

    // ── Reservation combinators: AST shape ───────────────────────────────────
    //
    // These pin the parser output that the TypeCheckerTests for SEQ/AND/OR
    // reservation combinators rely on. Without these tests, a parser regression
    // could hide behind a typechecker that still happens to accept the source.

    [Fact]
    public void ReserveSeqReserve_IsBinarySeqOfTwoReserves()
    {
        // "reserve myRoom from … to … seq reserve myRoom from … to …" must
        // produce BinaryOperation(SEQ, Reserve, Reserve) as the assignment RHS.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidReserveSeqReserve);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);

        var seq = Assert.IsType<BinaryOperation>(rhs);
        Assert.Equal(BinaryOperator.SEQ, seq.Operator);
        Assert.IsType<Reserve>(seq.LeftExpression);
        Assert.IsType<Reserve>(seq.RightExpression);
    }

    [Fact]
    public void ReserveAndReserve_IsBinaryAndOfTwoReserves()
    {
        // "reserve … and reserve …" must produce BinaryOperation(AND, Reserve, Reserve).
        // Distinguishes the reservation-combinator AND from the boolean AND, since
        // the grammar reuses the same keyword and BinaryOperator.AND for both.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidReserveAndReserve);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);

        var and = Assert.IsType<BinaryOperation>(rhs);
        Assert.Equal(BinaryOperator.AND, and.Operator);
        Assert.IsType<Reserve>(and.LeftExpression);
        Assert.IsType<Reserve>(and.RightExpression);
    }

    // ── Recurring reservation: AST shape ─────────────────────────────────────

    [Fact]
    public void RecurringStrictUntil_PopulatesRecurrenceSpec()
    {
        // "reserve … recurring strict every 1 week until 30/06-2026" must populate
        //   Reserve.Query.Recurrence = RecurrenceSpec(
        //     Mode = STRICT,
        //     Time = RecurrenceUntil(Every = DurationV(7 days),
        //                            Until = DateTimeV(30/06/2026)))
        // Pins the parser output that RecurrenceIsWellTyped walks.
        Stmt root = TestHelpers.ParseShouldSucceed(TestPrograms.ValidRecurringStrictUntil);
        Exp rhs = TestHelpers.ExtractFirstAssignmentRhs(root);

        var reserve = Assert.IsType<Reserve>(rhs);
        Assert.NotNull(reserve.Query.Recurrence);
        var recurrence = reserve.Query.Recurrence!;
        Assert.Equal(RecurrenceMode.STRICT, recurrence.Mode);

        // "until" form parses to RecurrenceUntil (not RecurrenceFor).
        var until = Assert.IsType<RecurrenceUntil>(recurrence.Time);

        var every = Assert.IsType<DurationV>(until.Every);
        Assert.Equal(TimeSpan.FromDays(7), every.Value);

        var endDate = Assert.IsType<DateTimeV>(until.EndMarker);
        Assert.Equal(new DateTime(2026, 6, 30), endDate.Value.Date);
    }
}
