using RAL.AST;
using RAL.TC;

namespace RAL.Tests;

/*
 * Test level: mixed.
 *   - 'Direct AST construction' sub-sections are unit-flavoured
 *     (TypeChecker visitor on a hand-built node, no parser).
 *   - 'Source programs' sub-sections are integration-flavoured
 *     (parser + typechecker, run together).
 *
 * Per Thomsen, this fluidity is normal in compiler projects: a typechecker
 * visitor needs an AST and recurses through sibling visitors, so its tests
 * often "have the flavor of integration tests."
 *
 * Tests for the TypeChecker.
 * Every program tested here must parse correctly;
 * only semantic/type rules are under scrutiny.
 */
public class TypeCheckerTests
{
    // ── Positive: programs that must typecheck without errors ────────────────

    [Fact]
    public void NumberArithmetic_IsAccepted()
    {
        // "Number x = 2 / 5 * (2 + 2);" — beyond "no errors", the var-decl rule
        // must have bound x in envV with type NumberT. A regression in HandleVarDecl
        // that silently skipped the bind would still pass Assert.Empty(tc.errors)
        // but fail the env-state probe below.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidArithmetic);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<NumberT>(r.EnvV.Lookup("x"));
    }

    [Fact]
    public void BoolLiteral_IsAccepted()
    {
        // Bind rule: "Bool b = true;" → envV.b must be BoolT.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidBoolLiteral);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<BoolT>(r.EnvV.Lookup("b"));
    }

    [Fact]
    public void StringDecl_IsAccepted()
    {
        // Bind rule: """String s = "hello";""" → envV.s must be StringT.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidStringDecl);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<StringT>(r.EnvV.Lookup("s"));
    }

    [Fact]
    public void MultipleNumberDecls_AreAccepted()
    {
        // Three sibling VarDecls in one Composite tree must all bind. Probing each
        // name individually pins composite-traversal: a regression that processed
        // only Stmt1 (or only Stmt2) of the right-leaning Composite would still
        // report zero errors but leave one or two names unbound.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidMultipleDecls);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<NumberT>(r.EnvV.Lookup("x"));
        Assert.IsType<NumberT>(r.EnvV.Lookup("y"));
        Assert.IsType<NumberT>(r.EnvV.Lookup("z"));
    }

    [Fact]
    public void BoolExpression_IsAccepted()
    {
        // "Bool b = true and false;" — the RHS must type to BoolT and the bind
        // must succeed. Asserting envV.b = BoolT confirms both: a wrong RHS type
        // would have been caught by the assignment-compatibility rule and added
        // to tc.errors, so reaching BoolT in envV implies the AND-on-Bools rule
        // returned BoolT as intended.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidBoolExpr);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<BoolT>(r.EnvV.Lookup("b"));
    }

    [Fact]
    public void ResourceAndTemplate_AreAccepted()
    {
        // The source declares three independent things: a category Room, a
        // resource myRoom in Room, and a template booking(Number, String).
        // "no errors" alone cannot distinguish a category-binding regression
        // from a resource-binding regression from a template-signature
        // regression. Probing each env (envC, envV, envT) pins all three
        // independently — a failure in one rule fails its own assertion line.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidResourceTemplate);
        Assert.Empty(r.Tc.errors);

        // category Room
        Assert.True(r.EnvC.CategoryIsDeclared("Room"));

        // Room myRoom {}
        Assert.Equal(new ResourceT("Room"), r.EnvV.Lookup("myRoom"));

        // template booking(Number qty, String label) {...} — signature stored verbatim
        List<TypeT>? bookingSig = r.EnvT.Lookup("booking");
        Assert.NotNull(bookingSig);
        Assert.Equal(2, bookingSig!.Count);
        Assert.IsType<NumberT>(bookingSig[0]);
        Assert.IsType<StringT>(bookingSig[1]);
    }

    [Fact]
    public void ResourceWithProperty_IsAccepted()
    {
        // "category Room; Room myRoom { Number beds = 2; }" — property-binding
        // touches three envs: envV (the resource variable), envR (resource's
        // field map), and envCPT (category's property table for where-clause
        // typing). All three must agree on the field's type. Asserting on each
        // tier pins exactly where a regression would sit.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidResourceWithProperty);
        Assert.Empty(r.Tc.errors);

        Assert.Equal(new ResourceT("Room"), r.EnvV.Lookup("myRoom"));
        Assert.True(r.EnvR.HasField("myRoom", "beds"));
        Assert.IsType<NumberT>(r.EnvR.LookupField("myRoom", "beds"));
        Assert.True(r.EnvCPT.HasProperty("Room", "beds"));
    }

    [Fact]
    public void CategoryHierarchy_IsAccepted()
    {
        // "category Room; category DoubleRoom is a Room;" — the subtype relation
        // must end up in envH so later resource-typing can resolve DoubleRoom-as-Room.
        // Probing envC pins category registration; envH.IsSubtype pins the "is a"
        // edge specifically — a regression that registered DoubleRoom without
        // recording its parent would pass the envC assertion but fail IsSubtype.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidCategoryHierarchy);
        Assert.Empty(r.Tc.errors);

        Assert.True(r.EnvC.CategoryIsDeclared("Room"));
        Assert.True(r.EnvC.CategoryIsDeclared("DoubleRoom"));
        Assert.True(r.EnvH.IsSubtype(new ResourceT("DoubleRoom"), new ResourceT("Room")));
    }

    [Fact]
    public void IfStatement_WithBoolCondition_IsAccepted()
    {
        // "Bool cond = true; if (cond) then {...} else {...}" — cond must be
        // bound as BoolT in the outer scope before HandleIf inspects it. Probing
        // envV.cond pins the condition-binding precondition; the if-rule itself
        // only adds an error when the condition is non-Bool, so empty errors
        // covers the if-rule's own outcome.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidIfStmt);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<BoolT>(r.EnvV.Lookup("cond"));
    }

    [Fact]
    public void Shadowing_TemplateParamShadowsOuterVar_IsAccepted()
    {
        // "Number x = 5; template t(Number x) { Number y = x; }" — the template
        // body opens a child scope, so the inner x (the parameter) shadows the
        // outer x without overwriting it. After typecheck the outer envV must
        // still see x as NumberT (proves shadow was scoped, not assigned), and
        // envT must hold t with the single Number parameter (proves the
        // template signature itself was registered correctly).
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidShadowing);
        Assert.Empty(r.Tc.errors);

        Assert.IsType<NumberT>(r.EnvV.Lookup("x"));
        List<TypeT>? tSig = r.EnvT.Lookup("t");
        Assert.NotNull(tSig);
        Assert.Single(tSig!);
        Assert.IsType<NumberT>(tSig[0]);
    }

    [Fact]
    public void TemplateBody_ReadsOuterScopeVariable_IsAccepted()
    {
        // "Number outer = 5; template t() { Number inner = outer; }" — the
        // template body must resolve "outer" by walking from its local envV up
        // through parent links into the enclosing envV. A regression that
        // failed to chain the lookup would emit "undeclared variable 'outer'"
        // (caught by Assert.Empty). Probing envV.outer = NumberT confirms the
        // outer binding survived; envT.Lookup("t") with empty signature confirms
        // template registration on a zero-param declaration.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidTemplateBodyReadsOuter);
        Assert.Empty(r.Tc.errors);

        Assert.IsType<NumberT>(r.EnvV.Lookup("outer"));
        List<TypeT>? tSig = r.EnvT.Lookup("t");
        Assert.NotNull(tSig);
        Assert.Empty(tSig!);
    }

    // ── Positive: direct AST construction ────────────────────────────────────

    [Fact]
    public void UnaryNot_OnBool_IsAccepted()
    {
        // UnaryOperation(NOT, BoolV(false)) → BoolT — no type error.
        var node = new ExpStmt(1, new UnaryOperation(1, UnaryOperator.NOT, new BoolV(1, false)));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.Empty(tc.errors);
    }

    [Fact]
    public void BinaryAdd_TwoNumbers_IsAccepted()
    {
        var node = new ExpStmt(1,
            new BinaryOperation(1, new NumberV(1, 2), BinaryOperator.ADD, new NumberV(1, 3)));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.Empty(tc.errors);
    }

    [Fact]
    public void BinaryEq_TwoBools_IsAccepted()
    {
        var node = new ExpStmt(1,
            new BinaryOperation(1, new BoolV(1, true), BinaryOperator.EQ, new BoolV(1, false)));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.Empty(tc.errors);
    }

    // ── Negative: programs that must produce at least one type error ──────────

    [Fact]
    public void StringAssignedToNumber_IsRejected()
    {
        // """Number x = "hello";""" — assignment-compat rule emits
        //   "Variable 'x' expected type 'Number' got 'String'."
        // Pinning the "expected type 'Number'" + "got 'String'" phrases
        // identifies this exact rule, not any incidental error mentioning
        // the words "number" or "string".
        TestHelpers.TypeCheckShouldReportError(
            TestPrograms.InvalidTypeStringAssignedToNumber,
            "expected type 'Number'", "got 'String'");
    }

    [Fact]
    public void BoolDivision_IsRejected()
    {
        // "Number s = true / false;" — the DIV rule emits
        //   "Operand types 'Bool' and 'Bool' incompatible for operator '/'."
        // Pinning the operand-pair phrase + the operator together identifies
        // the DIV-on-Bools branch specifically. (The bare keyword "bool" used
        // previously would match almost any Bool-related error message.)
        TestHelpers.TypeCheckShouldReportError(
            TestPrograms.InvalidTypeBoolDivision,
            "Operand types 'Bool' and 'Bool'", "operator '/'");
    }

    [Fact]
    public void IfStatement_WithNonBoolCondition_IsRejected()
    {
        // "Number n = 5; if (n) then {...}" — HandleIf emits
        //   "If statement expected condition of type 'bool' got 'Number'."
        // Pinning "If statement" + "condition" + "got 'Number'" identifies
        // the if-condition rule and the offending type together.
        TestHelpers.TypeCheckShouldReportError(
            TestPrograms.InvalidTypeIfNonBoolCondition,
            "If statement", "condition", "got 'Number'");
    }

    // ── Negative: direct AST construction ────────────────────────────────────

    [Fact]
    public void UnaryNot_OnNumber_IsRejected()
    {
        // UnaryOperation(NOT, NumberV(5)) → typechecker must add an error.
        // The NOT-on-non-Bool rule emits a message of the form
        //   "Operator 'not' expected 'Bool' got 'Number'."
        // Each of the three identifying phrases is asserted separately so a
        // failure points at exactly which token drifted: if the production
        // wording changes "expected" to "wanted", only that one Assert.Contains
        // line fails and the stack trace names it.
        var node = new ExpStmt(1, new UnaryOperation(1, UnaryOperator.NOT, new NumberV(1, 5)));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.NotEmpty(tc.errors);
        Assert.Contains(tc.errors, e => e.Contains("Operator 'not'",   StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tc.errors, e => e.Contains("expected 'Bool'",  StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tc.errors, e => e.Contains("got 'Number'",     StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BinaryDiv_BoolOperands_IsRejected()
    {
        // BinaryOperation(DIV, BoolV, BoolV) → type error.
        // Binary operand-type errors take the form
        //   "Operand types 'L' and 'R' incompatible for operator 'OP'."
        // Operand-pair phrase and operator are asserted separately; a stray
        // production change to either component fails its own assertion only.
        var node = new ExpStmt(1,
            new BinaryOperation(1, new BoolV(1, true), BinaryOperator.DIV, new BoolV(1, false)));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.NotEmpty(tc.errors);
        Assert.Contains(tc.errors, e => e.Contains("Operand types 'Bool' and 'Bool'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tc.errors, e => e.Contains("operator '/'",                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BinaryAdd_StringAndNumber_IsRejected()
    {
        // BinaryOperation(ADD, StringV, NumberV) → type error.
        var node = new ExpStmt(1,
            new BinaryOperation(1, new StringV(1, "hello"), BinaryOperator.ADD, new NumberV(1, 3)));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.NotEmpty(tc.errors);
        Assert.Contains(tc.errors, e => e.Contains("Operand types 'String' and 'Number'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tc.errors, e => e.Contains("operator '+'",                        StringComparison.OrdinalIgnoreCase));
    }

    // ── Unary NEG: TypeChecker accepts/rejects correctly (direct AST) ─────────

    [Fact]
    public void UnaryNeg_OnNumber_IsAccepted_DirectAst()
    {
        // UnaryOperation(NEG, NumberV(5)) → NumberT — no type error.
        var node = new ExpStmt(1, new UnaryOperation(1, UnaryOperator.NEG, new NumberV(1, 5)));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.Empty(tc.errors);
    }

    [Fact]
    public void UnaryNeg_OnBool_IsRejected_DirectAst()
    {
        // UnaryOperation(NEG, BoolV(true)) → type error (NEG requires Number).
        // The NEG-on-non-Number rule emits a message of the form
        //   "Operator '-' expected 'Number' got 'Bool'."
        // Each phrase asserted separately for focused failure diagnostics.
        var node = new ExpStmt(1, new UnaryOperation(1, UnaryOperator.NEG, new BoolV(1, true)));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.NotEmpty(tc.errors);
        Assert.Contains(tc.errors, e => e.Contains("Operator '-'",      StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tc.errors, e => e.Contains("expected 'Number'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tc.errors, e => e.Contains("got 'Bool'",        StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NotOnNumber_Pipeline_ShouldBeRejectedByTypechecker()
    {
        // "Number n = not(5);" should:
        //   1. Parse successfully (syntax is valid).
        //   2. Produce UnaryOperation(NOT, NumberV(5)) in the AST.
        //   3. Be rejected by the typechecker because NOT requires Bool.
        // HandleUnary's NOT branch emits
        //   "Operator 'not' expected 'Bool' got 'Number'."
        // Same pinning style as the direct-AST companion UnaryNot_OnNumber_IsRejected.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.NotOnNumber,
            "Operator 'not'", "expected 'Bool'", "got 'Number'");
    }

    // ── Semantic errors: undeclared and duplicate identifiers ─────────────────
    // Intended behavior: these must be reported through tc.errors, not exceptions.
    // If the typechecker currently throws for any of these, the test will FAIL —
    // that failure is the signal that the typechecker needs to be fixed.

    [Fact]
    public void DuplicateVariable_SameScope_IsRejectedWithSemanticError()
    {
        // Binding the same identifier twice in the same scope must add an error
        // to tc.errors. Intended behavior: errors.Add, not a thrown exception.
        // Keyword "already declared" pins the duplicate-binding rule specifically;
        // a stray error from a different rule wouldn't include that phrase.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidTypeDuplicateVar,
            "already declared");
    }

    [Fact]
    public void UndeclaredVariable_IsRejectedWithSemanticError()
    {
        // Looking up an identifier that was never declared must add an error
        // to tc.errors. Intended behavior: errors.Add, not a thrown exception.
        // Keyword "undeclared variable" distinguishes this from "undeclared
        // category" / "undeclared template" — different rules, same source style.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidTypeUndeclaredVar,
            "undeclared variable");
    }

    // ── Positive: DateTime / Duration — source programs ──────────────────────

    [Fact]
    public void DateTimeDeclaration_IsAccepted()
    {
        // "DateTime dt = 15/03-2026;" — the literal-typing rule must yield
        // DateTimeT and the var-decl must bind dt to that type. A regression
        // that misclassified the literal would either be caught by the
        // assignment-compatibility rule (and added to tc.errors) or, if the
        // bind was still attempted, the env probe below would surface the
        // wrong type.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidDateTimeDeclaration);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<DateTimeT>(r.EnvV.Lookup("dt"));
    }

    [Fact]
    public void DurationDeclaration_IsAccepted()
    {
        // "Duration dur = 2 days;" — Duration literal must type to DurationT
        // and the bind must succeed.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidDurationDeclaration);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<DurationT>(r.EnvV.Lookup("dur"));
    }

    [Fact]
    public void DateTimePlusDuration_IsAccepted()
    {
        // Three sequential decls; the third asserts the binary rule
        // ADD(DateTimeT, DurationT) → DateTimeT. Probing endDate isolates the
        // binary rule's output: a regression in HandleBinary that returned the
        // wrong type would still pass "no errors" if the assignment-compat
        // rule's fallthrough didn't notice, and the env probe pins this.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidDateTimePlusDuration);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<DateTimeT>(r.EnvV.Lookup("startDate"));
        Assert.IsType<DurationT>(r.EnvV.Lookup("period"));
        Assert.IsType<DateTimeT>(r.EnvV.Lookup("endDate"));
    }

    [Fact]
    public void DateTimeMinusDuration_IsAccepted()
    {
        // Same shape as DateTimePlusDuration, but for SUB(DateTimeT, DurationT)
        // → DateTimeT. The two operands and the result type are pinned
        // separately so a SUB-specific regression surfaces on the endDate line.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidDateTimeMinusDuration);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<DateTimeT>(r.EnvV.Lookup("startDate"));
        Assert.IsType<DurationT>(r.EnvV.Lookup("period"));
        Assert.IsType<DateTimeT>(r.EnvV.Lookup("endDate"));
    }

    [Fact]
    public void DateTimeComparison_LessThan_IsAccepted()
    {
        // LT(DateTimeT, DateTimeT) → BoolT — probing isBefore = BoolT proves
        // the ordering-on-DateTimes rule returned Bool, not just that no error
        // fired.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidDateTimeComparison);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<BoolT>(r.EnvV.Lookup("isBefore"));
    }

    [Fact]
    public void DurationComparison_LessThan_IsAccepted()
    {
        // LT(DurationT, DurationT) → BoolT — symmetric companion to the
        // DateTime case above.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidDurationComparison);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<BoolT>(r.EnvV.Lookup("isBriefer"));
    }

    // ── Positive: DateTime / Duration — direct AST ────────────────────────────
    // The two EQ tests below are positive
    // coverage of EQ on DateTimes / Durations anywhere in the suite. They are
    // wrapped in a typed VarDecl + Assignment so the assignment-compat rule
    // indirectly verifies that EQ actually returns BoolT — a regression that
    // returned DateTimeT or DurationT instead would now surface as an
    // "expected type 'Bool' got '...'" error.

    [Fact]
    public void BinaryEq_TwoDateTimes_IsAccepted()
    {
        // Direct-AST equivalent of "Bool b = 15/03-2026 == 16/03-2026;".
        // EQ(DateTimeT, DateTimeT) → BoolT, then HandleAssignment compares the
        // returned type against the declared BoolT for variable b. If EQ
        // regressed to return DateTimeT instead, assignment-compat would emit
        //   "Variable 'b' expected type 'Bool' got 'Datetime'."
        // so Assert.Empty(tc.errors) actually verifies the BoolT result.
        var stmt = new Composite(1,
            new VarDecl(1, new BoolT(), "b"),
            new ExpStmt(1, new Assignment(1,
                new Reference(1, "b", null),
                new BinaryOperation(1,
                    new DateTimeV(1, new DateTime(2026, 3, 15)),
                    BinaryOperator.EQ,
                    new DateTimeV(1, new DateTime(2026, 3, 16))))));
        TypeChecker tc = TestHelpers.RunTypeChecker(stmt);
        Assert.Empty(tc.errors);
    }

    [Fact]
    public void BinaryEq_TwoDurations_IsAccepted()
    {
        // Symmetric to BinaryEq_TwoDateTimes_IsAccepted but for EQ(DurationT,
        // DurationT) → BoolT. Same wrapping rationale: the result type is
        // pinned by the assignment-compat check against the declared BoolT.
        var stmt = new Composite(1,
            new VarDecl(1, new BoolT(), "b"),
            new ExpStmt(1, new Assignment(1,
                new Reference(1, "b", null),
                new BinaryOperation(1,
                    new DurationV(1, TimeSpan.FromHours(1)),
                    BinaryOperator.EQ,
                    new DurationV(1, TimeSpan.FromHours(2))))));
        TypeChecker tc = TestHelpers.RunTypeChecker(stmt);
        Assert.Empty(tc.errors);
    }

    // ── Negative: DateTime / Duration — must produce type errors ─────────────

    [Fact]
    public void DateTimePlusDateTime_IsRejected()
    {
        // ADD(DateTimeT, DateTimeT).
        // "Operand types 'Datetime' and 'Datetime' incompatible for operator '+'."
        TestHelpers.TypeCheckShouldReportError(
            TestPrograms.InvalidDateTimePlusDateTime,
            "Operand types 'Datetime' and 'Datetime'", "operator '+'");
    }

    [Fact]
    public void DateTimeMinusDateTime_IsRejected()
    {
        // SUB only accepts (DateTimeT, DurationT); (DateTimeT, DateTimeT) falls
        // through to the standard "Operand types ... incompatible for operator '-'."
        TestHelpers.TypeCheckShouldReportError(
            TestPrograms.InvalidDateTimeMinusDateTime,
            "Operand types 'Datetime' and 'Datetime'", "operator '-'");
    }

    [Fact]
    public void DurationPlusDateTime_IsRejected()
    {
        // Operand order matters: ADD(DurationT, DateTimeT) is rejected even
        // though (DateTimeT, DurationT) is accepted. Pinning both operand
        // types in source order proves the rule rejected the wrong-direction
        // pairing specifically.
        TestHelpers.TypeCheckShouldReportError(
            TestPrograms.InvalidDurationPlusDateTime,
            "Operand types 'Duration' and 'Datetime'", "operator '+'");
    }

    [Fact]
    public void DateTimeAssignedToNumber_IsRejected()
    {
        // "Number n = 15/03-2026;" — assignment-compat rule emits
        //   "Variable 'n' expected type 'Number' got 'Datetime'."
        TestHelpers.TypeCheckShouldReportError(
            TestPrograms.InvalidDateTimeAssignedToNumber,
            "expected type 'Number'", "got 'Datetime'");
    }

    [Fact]
    public void DurationAssignedToDateTime_IsRejected()
    {
        // "DateTime dt = 2 days;" — assignment-compat rule emits
        //   "Variable 'dt' expected type 'Datetime' got 'Duration'."
        TestHelpers.TypeCheckShouldReportError(
            TestPrograms.InvalidDurationAssignedToDateTime,
            "expected type 'Datetime'", "got 'Duration'");
    }

    [Fact]
    public void BinaryAdd_DateTimePlusDateTime_IsRejected_DirectAst()
    {
        // ADD(DateTimeT, DateTimeT) has no overload — the rule emits
        //   "Operand types 'Datetime' and 'Datetime' incompatible for operator '+'."
        var node = new ExpStmt(1, new BinaryOperation(1,
            new DateTimeV(1, new DateTime(2026, 3, 15)),
            BinaryOperator.ADD,
            new DateTimeV(1, new DateTime(2026, 3, 16))));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.NotEmpty(tc.errors);
        Assert.Contains(tc.errors, e => e.Contains("Operand types 'Datetime' and 'Datetime'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tc.errors, e => e.Contains("operator '+'",                            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BinaryAdd_DurationPlusDateTime_IsRejected_DirectAst()
    {
        // Operand order matters: ADD(DurationT, DateTimeT) is rejected even
        // though (DateTimeT, DurationT) is accepted. Pinning both operand
        // types in source order proves the rule rejected the wrong-direction
        // pairing specifically.
        var node = new ExpStmt(1, new BinaryOperation(1,
            new DurationV(1, TimeSpan.FromDays(1)),
            BinaryOperator.ADD,
            new DateTimeV(1, new DateTime(2026, 3, 15))));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.NotEmpty(tc.errors);
        Assert.Contains(tc.errors, e => e.Contains("Operand types 'Duration' and 'Datetime'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tc.errors, e => e.Contains("operator '+'",                            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BinaryLt_DateTimeAndNumber_IsRejected_DirectAst()
    {
        // DateTime compared with Number → type error. Ordering operators only
        // accept homogeneous pairs (two DateTimes, two Durations, two Numbers,
        // two Strings); mixed pairings are rejected with the standard
        // "Operand types … incompatible for operator …" template.
        var node = new ExpStmt(1, new BinaryOperation(1,
            new DateTimeV(1, new DateTime(2026, 3, 15)),
            BinaryOperator.LT,
            new NumberV(1, 5)));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.NotEmpty(tc.errors);
        Assert.Contains(tc.errors, e => e.Contains("Operand types 'Datetime' and 'Number'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tc.errors, e => e.Contains("operator '<'",                          StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BinaryEq_DurationAndString_IsRejected_DirectAst()
    {
        // Duration compared with String → type error. Same template as the
        // ordering case above; pin operand types and operator together.
        var node = new ExpStmt(1, new BinaryOperation(1,
            new DurationV(1, TimeSpan.FromDays(1)),
            BinaryOperator.EQ,
            new StringV(1, "hello")));
        TypeChecker tc = TestHelpers.RunTypeChecker(node);
        Assert.NotEmpty(tc.errors);
        Assert.Contains(tc.errors, e => e.Contains("Operand types 'Duration' and 'String'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tc.errors, e => e.Contains("operator '=='",                         StringComparison.OrdinalIgnoreCase));
    }

    // ── Positive: core RAL semantics ─────────────────────────────────────────
    //
    // ResourceDeclWithProperties_IsAccepted and CategoryInheritance_IsAccepted
    // were removed here: both were aliases of ResourceWithProperty_IsAccepted
    // and CategoryHierarchy_IsAccepted above. The strengthened originals already
    // probe envV/envR/envCPT and envC/envH respectively.

    [Fact]
    public void MoveResource_ToValidCategory_IsAccepted()
    {
        // "category Room; category Suite is a Room; Room myRoom {}; move myRoom to Suite;"
        // The move rule mutates the resource variable's type in envV from
        // ResourceT("Room") to ResourceT("Suite"). Probing envV.myRoom = Suite
        // confirms the mutation.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidMoveResourceToCategory);
        Assert.Empty(r.Tc.errors);
        Assert.Equal(new ResourceT("Suite"), r.EnvV.Lookup("myRoom"));
        Assert.True(r.EnvH.IsSubtype(new ResourceT("Suite"), new ResourceT("Room")));
    }

    [Fact]
    public void ReserveKnownResource_IsAccepted()
    {
        // "Reservation res = reserve myRoom from ... to ...;" — the reserve
        // expression must type to ReservationT, then the assignment-compat
        // rule must accept it for the Reservation-typed declaration. Probing
        // envV.res = ReservationT confirms both stages.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidReserveStatement);
        Assert.Empty(r.Tc.errors);
        Assert.Equal(new ResourceT("Room"), r.EnvV.Lookup("myRoom"));
        Assert.IsType<ReservationT>(r.EnvV.Lookup("res"));
    }

    [Fact]
    public void AvailabilityKnownResource_IsAccepted()
    {
        // "check myRoom from ... to ...;" — Availability is a statement with
        // no envV side effect, so the only env-state probe we have is the
        // precondition that myRoom was bound before the check. The Availability
        // rule itself only adds errors when the query is malformed, so empty
        // errors covers the check-statement's own outcome.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidAvailabilityQuery);
        Assert.Empty(r.Tc.errors);
        Assert.Equal(new ResourceT("Room"), r.EnvV.Lookup("myRoom"));
    }

    [Fact]
    public void CancelReservation_IsAccepted()
    {
        // Cancel rule requires the operand to be a Reservation variable.
        // Probing envV.res = ReservationT proves the reserve before cancel
        // bound correctly — cancel itself does not unbind the variable at
        // type-time (binding stays; only its runtime value flips to "failed").
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidCancelReservation);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<ReservationT>(r.EnvV.Lookup("res"));
    }

    [Fact]
    public void RescheduleReservation_IsAccepted()
    {
        // "reschedule res from ... to ..." returns ReservationT and is assigned
        // to a new Reservation-typed variable. Both bindings must survive — a
        // regression in the reschedule rule that returned a different type
        // would fail the assignment-compat check on rescheduled.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidRescheduleReservation);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<ReservationT>(r.EnvV.Lookup("res"));
        Assert.IsType<ReservationT>(r.EnvV.Lookup("rescheduled"));
    }

    [Fact]
    public void PropertyAccess_KnownField_IsAccepted()
    {
        // "Number numBeds = myRoom.beds;" — property-access on a known field
        // must yield the field's declared type (NumberT here). The RHS-type
        // → LHS-type chain is pinned by probing numBeds = NumberT: if
        // HandleReference returned the wrong type for property access, the
        // assignment-compat rule would have caught it (covered by Assert.Empty),
        // OR the bind would carry a wrong type (covered by the IsType assertion).
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidResourcePropertyAccess);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<NumberT>(r.EnvV.Lookup("numBeds"));
        Assert.True(r.EnvR.HasField("myRoom", "beds"));
    }

    // ── Negative: core RAL semantics ─────────────────────────────────────────

    [Fact]
    public void CancelNonReservation_IsRejected()
    {
        // "Number n = 5; cancel n;" — HandleCancel emits
        //   "Expected type 'Reservation' got: Number."
        // Pin "Expected type 'Reservation'" + the offending type "got: Number"
        // — identifies the cancel-rule branch specifically, not any incidental
        // error mentioning the words "reservation" or "number".
        TestHelpers.TypeCheckShouldReportError(
            TestPrograms.InvalidCancelNonReservation,
            "Expected type 'Reservation'", "got: Number");
    }

    [Fact]
    public void MoveNonResource_IsRejected()
    {
        // "Number n = 5; category Room; move n to Room;" — HandleMove emits
        //   "Expected type 'Resource' got 'Number'."
        // The bare keyword "resource" used previously was too generic — it
        // matched almost any resource-related error. Pin the full rule phrase
        // and the offending operand type.
        TestHelpers.TypeCheckShouldReportError(
            TestPrograms.InvalidMoveNonResource,
            "Expected type 'Resource'", "got 'Number'");
    }

    [Fact]
    public void MoveUnknownResource_IsRejectedWithSemanticError()
    {
        // Moving an undeclared resource must add an error to tc.errors
        // (errors.Add, not a thrown exception). HandleMove emits
        //   "Use of undeclared variable 'ghost'."
        // Pin the rule phrase and the offending identifier so the assertion
        // identifies this branch specifically, not any incidental error
        // mentioning "ghost".
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidMoveUnknownResource,
            "undeclared variable", "ghost");
    }

    [Fact]
    public void MoveToUnknownCategory_IsRejectedWithSemanticError()
    {
        // Moving to an undeclared category must add an error to tc.errors.
        // The Move rule emits "Use of undeclared category 'Unknown'." for the
        // target name 'Unknown'. Pinning both the rule phrase and the
        // offending identifier confirms the right rule was applied on the right node.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidMoveUnknownCategory,
            "undeclared category", "Unknown");
    }

    [Fact]
    public void ReserveUnknownResource_IsRejectedWithSemanticError()
    {
        // Reserving an undeclared resource must add an error to tc.errors.
        // The query-typing rule emits "Use of undeclared variable 'ghost'."
        // for the offending resource name 'ghost'. Pin both the rule phrase
        // and the identifier so the assertion identifies this rejection
        // specifically, not any incidental error mentioning "ghost".
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidReserveUnknownResource,
            "undeclared variable", "ghost");
    }

    [Fact]
    public void AvailabilityUnknownResource_IsRejectedWithSemanticError()
    {
        // Checking availability of an undeclared resource must add an error
        // to tc.errors. Same query-typing rule as the reserve case above:
        // "Use of undeclared variable 'ghost'.".
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidAvailabilityUnknownResource,
            "undeclared variable", "ghost");
    }

    [Fact]
    public void PropertyAccess_UnknownField_IsRejectedWithSemanticError()
    {
        // Accessing a field that does not exist on a declared resource must
        // add an error to tc.errors (errors.Add, not a thrown exception).
        // HandlePropertyReference emits
        //   "Property 'floors' doesn't exist in resource 'myRoom'."
        // Pin the rule phrase, the offending property and the resource it was
        // looked up on — three keywords together identify this exact branch.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidResourcePropertyAccess,
            "Property 'floors'", "doesn't exist", "myRoom");
    }

    [Fact]
    public void DuplicateResourceField_IsRejectedWithSemanticError()
    {
        // Two fields with the same name in one resource must add an error to
        // tc.errors. The property-binding rule emits
        //   "Property 'beds' has already been declared."
        // for the duplicate field 'beds'. Pin the rule keyword, the offending
        // identifier and the "already" phrase that distinguishes this from a
        // generic property-related error.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidDuplicateResourceField,
            "Property", "beds", "already");
    }

    [Fact]
    public void DuplicateCategory_IsRejected()
    {
        // "category Room; category Room;" — HandleCategoryDecl emits
        //   "Category 'Room' has already been declared."
        // Pin the rule phrase, the quoted category name, and the
        // "already been declared" suffix that distinguishes this from
        // other Room-related errors.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidDuplicateCategory,
            "Category 'Room'", "already been declared");
    }

    // ── Where-clause semantics ───────────────────────────────────────────────

    [Fact]
    public void ValidReserveWithWherePredicate_IsAccepted()
    {
        // "Reservation res = reserve 1 Room r ... where (r.beds == 2);"
        // The where-clause rule integrates several preconditions:
        //   - room1 must be a resource of category Room (envV)
        //   - Room.beds must be known to the category-property table (envCPT)
        //   - the predicate r.beds == 2 must type to Bool (covered by Assert.Empty:
        //     a non-Bool predicate would emit "expected 'bool' got 'X'.")
        //   - the whole reserve must yield ReservationT and bind res (envV)
        // Probing each precondition pins which rule fails if a regression
        // appears, instead of leaving "no errors" to cover all five.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidReserveWherePredicate);
        Assert.Empty(r.Tc.errors);

        Assert.Equal(new ResourceT("Room"), r.EnvV.Lookup("room1"));
        Assert.True(r.EnvCPT.HasProperty("Room", "beds"));
        Assert.IsType<NumberT>(r.EnvCPT.Lookup("Room", "beds"));
        Assert.IsType<ReservationT>(r.EnvV.Lookup("res"));
    }

    [Fact]
    public void ReserveWhereNonBoolPredicate_IsRejectedWithSemanticError()
    {
        // Predicate "r.beds + 2" has type Number; where clause requires Bool.
        // ConditionIsWellTyped emits
        //   "Condition expected type 'bool' got 'Number'."
        // Pin the rule phrase + the offending type so the keyword set
        // identifies the where-condition branch specifically (the bare keyword
        // "condition" or "bool" used previously matched several other rules).
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidReserveWhereNonBoolPredicate,
            "Condition", "expected type 'bool'", "got 'Number'");
    }

    [Fact]
    public void ReserveWhereUnknownProperty_IsRejectedWithSemanticError()
    {
        // 'floors' is not a declared property of any Room resource. The
        // category-property lookup emits
        //   "No resource in the category tree of 'Room' declares a field 'floors'."
        // Pin the offending property name and the rule keyword "field" so the
        // assertion identifies this specific rejection.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidReserveWhereUnknownProperty,
            "floors", "field");
    }

    [Fact]
    public void ReserveWhereUnknownAlias_IsRejectedWithSemanticError()
    {
        // Alias 'x' was never introduced in the resource spec; only 'r' was.
        // The reference-resolution rule emits "Use of undeclared variable 'x'."
        // Pin the rule phrase and the quoted alias — "'x'" with quotes rather
        // than bare "x", because the bare letter would match almost any error
        // message incidentally.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidReserveWhereUnknownAlias,
            "undeclared variable", "'x'");
    }

    // ── Template call semantics ───────────────────────────────────────────────

    [Fact]
    public void ValidTemplateCall_IsAccepted()
    {
        // "template booking(Number n, String label) {} use booking(2, \"meeting\");"
        // The template-decl rule registers the signature in envT; the
        // template-call rule then looks it up and matches the supplied
        // argument types against it. Probing envT.Lookup("booking") confirms
        // the signature was registered with exactly the declared types — a
        // regression that wrote the wrong types would still pass empty errors
        // if the call happened to pass the same wrong types.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidTemplateCall);
        Assert.Empty(r.Tc.errors);

        List<TypeT>? sig = r.EnvT.Lookup("booking");
        Assert.NotNull(sig);
        Assert.Equal(2, sig!.Count);
        Assert.IsType<NumberT>(sig[0]);
        Assert.IsType<StringT>(sig[1]);
    }

    [Fact]
    public void TemplateCall_WrongArgType_IsRejectedWithSemanticError()
    {
        // "template booking(Number n) {} use booking(\"wrong\");" — the per-arg
        // type-match rule emits
        //   "Argument 1 of template 'booking' expected 'Number' got 'String'."
        // Pin the offending arg position + template name + the type-mismatch
        // phrase so the keyword set identifies the per-arg branch — not the
        // count-mismatch branch immediately above it in the same handler.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidTemplateCallWrongArgType,
            "Argument 1 of template 'booking'", "expected 'Number'", "got 'String'");
    }

    [Fact]
    public void TemplateCall_WrongArgCount_IsRejectedWithSemanticError()
    {
        // Template expects 2 arguments but call supplies 1. The count-mismatch
        // branch emits
        //   "booking expected 2 argument(s) got 1."
        // Pin the expected count + got phrase together so this assertion can
        // never be satisfied by the per-arg type-mismatch branch (which would
        // not include "expected 2 argument(s)"). The typechecker returns after
        // reporting the count mismatch so no out-of-range indexing happens in
        // the per-arg loop.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidTemplateCallWrongArgCount,
            "booking expected 2 argument(s)", "got 1");
    }

    [Fact]
    public void TemplateCall_UnknownTemplate_IsRejectedWithSemanticError()
    {
        // Calling an undeclared template must surface through tc.errors, not
        // through an unhandled exception from envT.Lookup. The rule emits
        //   "Use of undeclared template 'missingTemplate'."
        // Pin the rule phrase and the offending template name.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidTemplateCallUnknownTemplate,
            "undeclared template", "missingTemplate");
    }

    // ── Reservation combinator semantics (seq / and / or on reservations) ───
    //
    // HandleBinary rules:
    //   AND/OR: (BoolT, BoolT) | (ReservationT, ReservationT) → operand type
    //   SEQ:    (ReservationT, ReservationT) only
    // Any other operand pairing must add an error to tc.errors.

    [Fact]
    public void ReserveSeqReserve_IsAccepted()
    {
        // "Reservation res = reserve … seq reserve …;" — SEQ(Reservation, Reservation)
        // must yield ReservationT and bind res. The SEQ rule has no Bool overload,
        // so reaching ReservationT in envV proves the reservation-typed overload
        // fired (not just that some overload fired).
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidReserveSeqReserve);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<ReservationT>(r.EnvV.Lookup("res"));
    }

    [Fact]
    public void ReserveOrReserve_IsAccepted()
    {
        // OR overloaded between (Bool, Bool) and (Reservation, Reservation).
        // res = ReservationT proves the reservation overload fired — a regression
        // that defaulted to the Bool overload would have been caught by the
        // assignment-compat rule (and added to tc.errors).
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidReserveOrReserve);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<ReservationT>(r.EnvV.Lookup("res"));
    }

    [Fact]
    public void ReserveAndReserve_IsAccepted()
    {
        // AND overloaded between (Bool, Bool) and (Reservation, Reservation).
        // Symmetric to the OR case above.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidReserveAndReserve);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<ReservationT>(r.EnvV.Lookup("res"));
    }

    [Fact]
    public void SeqOnBools_IsRejected()
    {
        // "Reservation res = true seq false;" — SEQ has no Bool overload, only
        // (Reservation, Reservation). HandleBinary's SEQ-fallthrough emits
        //   "Operand types 'Bool' and 'Bool' incompatible for operator 'seq'."
        // Same pinning style as the AND/OR/DIV operand-pair rejections — pin
        // operand-pair + the specific operator together.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidSeqOnBools,
            "Operand types 'Bool' and 'Bool'", "operator 'seq'");
    }

    // ── Recurring reservation semantics ──────────────────────────────────────
    //
    // RecurrenceIsWellTyped accepts:
    //   (DurationT, DateTimeT) for "every D until DT"
    //   (DurationT, DurationT) for "every D for D"
    // Anything else must add an error to tc.errors.

    [Fact]
    public void RecurringStrictUntilDateTime_IsAccepted()
    {
        // "Reservation res = reserve … recurring strict every 1 week until 30/06-2026;"
        // Recurrence-typing rule accepts (DurationT, DateTimeT) for the until-form.
        // res = ReservationT proves the whole reserve+recurrence chain typed
        // correctly — a recurrence regression that rejected the operand pair
        // would add an error (caught by Assert.Empty) and the reserve would
        // not have produced a Reservation to bind.
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidRecurringStrictUntil);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<ReservationT>(r.EnvV.Lookup("res"));
    }

    [Fact]
    public void RecurringFlexibleForDuration_IsAccepted()
    {
        // Same as above but for the for-Duration form: recurrence-typing rule
        // accepts (DurationT, DurationT) for "every D for D".
        var r = TestHelpers.RunTypeCheckPipeline(TestPrograms.ValidRecurringFlexibleFor);
        Assert.Empty(r.Tc.errors);
        Assert.IsType<ReservationT>(r.EnvV.Lookup("res"));
    }

    [Fact]
    public void RecurringEveryNumber_IsRejected()
    {
        // "recurring strict every 5 until 30/06-2026" — 5 has type Number, not
        // Duration. RecurrenceIsWellTyped's until-branch emits
        //   "Expected 'every 'Duration' until 'DateTime'' got 'every 'Number' until 'Datetime''."
        // Pin the expected-form phrase + the got-form prefix that names the
        // offending operand. Either phrase alone would match the symmetric
        // for-Duration error template; together they pin the until-branch
        // specifically with the Number operand.
        TestHelpers.TypeCheckShouldReportError(TestPrograms.InvalidRecurringNumberInterval,
            "Expected 'every 'Duration' until 'DateTime''", "got 'every 'Number'");
    }
}
