namespace RAL.Tests;

/*
 * Reusable RAL source-program strings used across all test classes.
 *
 * IMPORTANT — reserved keywords in RAL that cannot be used as identifiers:
 *   move, to, cancel, if, then, else, Number, Bool, String, Category,
 *   Reservation, DateTime, Duration, category, is, a, template, or, and,
 *   seq, not, true, false, reschedule, use, check, reserve, where, from,
 *   for, w, week, weeks, d, day, days, h, hour, hours, m, minute, minutes,
 *   recurring, strict, flexible, every, until
 *
 * All variable names below are chosen to avoid these keywords.
 */
static class TestPrograms
{
    // ── Valid programs ──────────────────────────────────────────────────────

    public const string ValidArithmetic =
        "Number x = 2 / 5 * (2 + 2);";

    public const string ValidNumberDecl =
        "Number x = 42;";

    public const string ValidBoolLiteral =
        "Bool b = true;";

    public const string ValidStringDecl =
        """String s = "hello";""";

    public const string ValidCategory =
        "category Room;";

    public const string ValidCategoryHierarchy =
        "category Room;\ncategory DoubleRoom is a Room;";

    public const string ValidResourceDecl =
        "category Room;\nRoom myRoom {}";

    public const string ValidResourceWithProperty =
        "category Room;\nRoom myRoom { Number beds = 2; }";

    public const string ValidResourceTemplate =
        """
        category Room;
        Room myRoom {}
        template booking(Number qty, String label) {
          Number count = qty;
        }
        """;

    public const string ValidNestedArithmetic =
        "Number x = 2 + 3 * 4;";

    public const string ValidParenArithmetic =
        "Number x = (2 + 3) * 4;";

    public const string ValidBoolExpr =
        "Bool b = true and false;";

    public const string ValidShadowing =
        "Number x = 5;\ntemplate t(Number x) {\n  Number y = x;\n}";

    public const string ValidIfStmt =
        "Bool cond = true;\nif (cond) then { Bool p = false; } else { Bool q = true; }";

    public const string ValidMultipleDecls =
        "Number x = 1;\nNumber y = 2;\nNumber z = 3;";

    // ── Unary-operator programs ──────────────────────────────────────────────

    // "not(false)" should produce UnaryOperation(NOT, BoolV(false)) and evaluate to BoolVal(true).
    public const string NotFalse =
        "Bool result = not(false);";

    // "not(true)" should produce UnaryOperation(NOT, BoolV(true)) and evaluate to BoolVal(false).
    public const string NotTrue =
        "Bool result = not(true);";

    // "not(5)" should be rejected by the typechecker because NOT requires Bool.
    public const string NotOnNumber =
        "Number n = not(5);";

    // "Number x = -5;" should produce UnaryOperation(NEG, NumberV(5)) and evaluate to NumberVal(-5).
    public const string UnaryMinusFive =
        "Number x = -5;";

    // ── DateTime / Duration programs ──────────────────────────────────────────
    //
    // DateTime literal format (from grammar):
    //   dateLit = digit digit '/' digit digit '-' digit digit digit digit
    //   timeLit = digit digit ':' digit digit
    //   Full example:  15/03-2026        (date only)
    //                  15/03-2026 14:00  (date + time)
    //
    // Duration literal format (from grammar):
    //   number unit, optionally combined e.g. 1 week 2 days 3 hours 30 minutes
    //   Units: w/week/weeks  d/day/days  h/hour/hours  m/minute/minutes

    // "DateTime dt = 15/03-2026;" — plain date literal, no time component.
    public const string ValidDateTimeDeclaration =
        "DateTime dt = 15/03-2026;";

    // "DateTime dt = 15/03-2026 14:00;" — date literal with optional time component.
    public const string ValidDateTimeWithTime =
        "DateTime dt = 15/03-2026 14:00;";

    // "Duration dur = 2 days;" — simple day-based duration.
    public const string ValidDurationDeclaration =
        "Duration dur = 2 days;";

    // "Duration dur = 1 week 2 days 3 hours 30 minutes;" — all four units combined.
    public const string ValidDurationCompound =
        "Duration dur = 1 week 2 days 3 hours 30 minutes;";

    // dt + dur → DateTime; all three variables in scope for the third assignment.
    public const string ValidDateTimePlusDuration =
        "DateTime startDate = 15/03-2026;\nDuration period = 2 days;\nDateTime endDate = startDate + period;";

    // dt - dur → DateTime; subtraction of a duration from a datetime.
    public const string ValidDateTimeMinusDuration =
        "DateTime startDate = 17/03-2026;\nDuration period = 2 days;\nDateTime endDate = startDate - period;";

    // Type error: ADD(DateTimeT, DateTimeT) is not a valid operation.
    public const string InvalidDateTimePlusDateTime =
        "DateTime begin = 15/03-2026;\nDateTime finish = 16/03-2026;\nDateTime sum = begin + finish;";

    // Type error: SUB only accepts (DateTimeT, DurationT), not (DateTimeT, DateTimeT).
    public const string InvalidDateTimeMinusDateTime =
        "DateTime begin = 16/03-2026;\nDateTime finish = 15/03-2026;\nDateTime diff = begin - finish;";

    // Type error: ADD(DurationT, DateTimeT) — operand order matters.
    public const string InvalidDurationPlusDateTime =
        "Duration period = 2 days;\nDateTime begin = 15/03-2026;\nDateTime result = period + begin;";

    // Relational comparison between two DateTimes → Bool.
    public const string ValidDateTimeComparison =
        "DateTime begin = 15/03-2026;\nDateTime finish = 16/03-2026;\nBool isBefore = begin < finish;";

    // Relational comparison between two Durations → Bool.
    public const string ValidDurationComparison =
        "Duration brief = 1 hour;\nDuration extended = 2 hours;\nBool isBriefer = brief < extended;";

    // Type error: DateTime literal assigned to a Number variable.
    public const string InvalidDateTimeAssignedToNumber =
        "Number n = 15/03-2026;";

    // Type error: Duration literal assigned to a DateTime variable.
    public const string InvalidDurationAssignedToDateTime =
        "DateTime dt = 2 days;";

    // ── Core RAL feature programs ─────────────────────────────────────────────

    // Move a resource to a declared subcategory.
    public const string ValidMoveResourceToCategory =
        "category Room;\ncategory Suite is a Room;\nRoom myRoom {}\nmove myRoom to Suite;";

    // Semantic error: resource identifier not declared. Typechecker must add an error to tc.errors.
    public const string InvalidMoveUnknownResource =
        "category Room;\nmove ghost to Room;";

    // Semantic error: target category not declared. Typechecker must add an error to tc.errors.
    public const string InvalidMoveUnknownCategory =
        "category Room;\nRoom myRoom {}\nmove myRoom to Unknown;";

    // Semantic error: move applied to a non-resource variable → added to errors list.
    public const string InvalidMoveNonResource =
        "Number n = 5;\ncategory Room;\nmove n to Room;";

    // Reserve a single named resource for a date-to-date interval.
    // "reserve myRoom from 15/03-2026 to 16/03-2026" is a Reserve expression.
    public const string ValidReserveStatement =
        "category Room;\nRoom myRoom {}\nReservation res = reserve myRoom from 15/03-2026 to 16/03-2026;";

    // "reserve myRoom from 15/03-2026 for 2 days" — the "for Duration" form of
    // the Time non-terminal, instead of the "to DateTime" form.
    public const string ValidReserveForDuration =
        "category Room;\nRoom myRoom {}\nReservation res = reserve myRoom from 15/03-2026 for 2 days;";

    // "reserve 2 Room from 15/03-2026 to 16/03-2026" — quantity+category resource
    // spec ("a*rc" form), no alias and no where clause.
    public const string ValidReserveQuantityCategory =
        "category Room;\nRoom myRoom {}\nReservation res = reserve 2 Room from 15/03-2026 to 16/03-2026;";

    // Availability check for a single named resource.
    // "check" is the keyword for the Availability statement.
    public const string ValidAvailabilityQuery =
        "category Room;\nRoom myRoom {}\ncheck myRoom from 15/03-2026 to 16/03-2026;";

    // Cancel a known Reservation variable.
    public const string ValidCancelReservation =
        "category Room;\nRoom myRoom {}\nReservation res = reserve myRoom from 15/03-2026 to 16/03-2026;\ncancel res;";

    // Reschedule an existing reservation to a new interval.
    // "reschedule" is a Primary expression returning ReservationT.
    public const string ValidRescheduleReservation =
        "category Room;\nRoom myRoom {}\nReservation res = reserve myRoom from 15/03-2026 to 16/03-2026;\n" +
        "Reservation rescheduled = reschedule res from 20/03-2026 to 21/03-2026;";

    // Type error: cancel applied to a non-Reservation expression → error in errors list.
    public const string InvalidCancelNonReservation =
        "Number n = 5;\ncancel n;";

    // Semantic error: reserve references an undeclared resource. Typechecker must add an error to tc.errors.
    public const string InvalidReserveUnknownResource =
        "category Room;\nReservation res = reserve ghost from 15/03-2026 to 16/03-2026;";

    // Semantic error: check references an undeclared resource. Typechecker must add an error to tc.errors.
    public const string InvalidAvailabilityUnknownResource =
        "category Room;\ncheck ghost from 15/03-2026 to 16/03-2026;";

    // Property access on a declared resource field that exists.
    public const string ValidResourcePropertyAccess =
        "category Room;\nRoom myRoom { Number beds = 2; }\nNumber numBeds = myRoom.beds;";

    // Semantic error: property access on a field that does not exist in the resource.
    public const string InvalidResourcePropertyAccess =
        "category Room;\nRoom myRoom {}\nNumber numFloors = myRoom.floors;";

    // ── Where-clause programs ────────────────────────────────────────────────
    //
    // Syntax reminder: where (Exp) — parentheses are required.
    // The alias bound in the resource spec (e.g. "r") is in scope inside the predicate.

    // Valid: alias r in scope, r.beds is a known Number field, r.beds == 2 is Bool.
    public const string ValidReserveWherePredicate =
        "category Room;\nRoom room1 { Number beds = 2; }\n" +
        "Reservation res = reserve 1 Room r from 15/03-2026 to 16/03-2026 where (r.beds == 2);";

    // Semantic error: predicate r.beds + 2 has type Number, not Bool.
    public const string InvalidReserveWhereNonBoolPredicate =
        "category Room;\nRoom room1 { Number beds = 2; }\n" +
        "Reservation res = reserve 1 Room r from 15/03-2026 to 16/03-2026 where (r.beds + 2);";

    // Semantic error: 'floors' is not a declared property of Room resources.
    public const string InvalidReserveWhereUnknownProperty =
        "category Room;\nRoom room1 { Number beds = 2; }\n" +
        "Reservation res = reserve 1 Room r from 15/03-2026 to 16/03-2026 where (r.floors == 2);";

    // Semantic error: alias 'x' was never introduced in the resource spec; 'r' was.
    public const string InvalidReserveWhereUnknownAlias =
        "category Room;\nRoom room1 { Number beds = 2; }\n" +
        "Reservation res = reserve 1 Room r from 15/03-2026 to 16/03-2026 where (x.beds == 2);";

    // ── Template call programs ───────────────────────────────────────────────

    // Valid: declaration then call — argument types match declared parameter types.
    public const string ValidTemplateCall =
        """
        template booking(Number n, String label) {}
        use booking(2, "meeting");
        """;

    // Type error: template expects Number for first arg but receives String.
    public const string InvalidTemplateCallWrongArgType =
        """
        template booking(Number n) {}
        use booking("wrong");
        """;

    // Semantic error: template expects 2 arguments but call supplies only 1.
    public const string InvalidTemplateCallWrongArgCount =
        """
        template booking(Number n, String label) {}
        use booking(2);
        """;

    // Semantic error: 'missingTemplate' was never declared.
    public const string InvalidTemplateCallUnknownTemplate =
        "use missingTemplate(2);";

    // A template that performs a reservation using its bound resource parameter.
    // After "use bookStay(roomA);" the outer variable "stay" must be a non-failed
    // ReservationVal whose single atom reserves exactly roomA from 15/03-2026 to
    // 17/03-2026 — proving that the template body actually executes against the
    // bound argument and produces an observable reservation side effect, not just
    // a primitive value assignment.
    public const string TemplateReservationBodyProgram =
        "category Room;\n" +
        "Room roomA {}\n" +
        "Reservation stay;\n" +
        "template bookStay(Room r) {\n" +
        "  stay = reserve r from 15/03-2026 to 17/03-2026;\n" +
        "}\n" +
        "use bookStay(roomA);";

    // The full UC10 compound booking pattern: a template that takes a room
    // parameter and produces TWO reservations in its body — a stay on the
    // parameter room, and a cleaning slot on the global cleaner immediately
    // after the stay. The "seq" combinator merges both atoms into one
    // composite ReservationVal, and the cleaning's start (17/03-2026 00:00)
    // is exactly the stay's end, encoding UC10's "immediately following the
    // stay" timing relation. Drives the same Parse → TypeCheck → Interpret
    // pipeline as the other template programs.
    public const string TemplateCompoundBookingProgram =
        "category Room;\n" +
        "category Cleaner;\n" +
        "Room roomA {}\n" +
        "Cleaner janitor {}\n" +
        "Reservation booking;\n" +
        "template bookStayWithClean(Room r) {\n" +
        "  booking = reserve r from 15/03-2026 to 17/03-2026 " +
        "seq reserve janitor from 17/03-2026 for 2 hours;\n" +
        "}\n" +
        "use bookStayWithClean(roomA);";

    // ── Invalid syntax ──────────────────────────────────────────────────────

    // Missing semicolon after statement.
    public const string InvalidSyntaxMissingSemicolon =
        "Number x = 5";

    // Declaration with an operator directly after identifier — bad grammar.
    public const string InvalidSyntaxBadDecl =
        "Number cd/2;";

    // Parenthesis never closed.
    public const string InvalidSyntaxUnclosedParen =
        "Number x = (5 + 3;";

    // ── Type-error programs (valid syntax, bad types) ────────────────────────

    public const string InvalidTypeStringAssignedToNumber =
        """Number x = "hello";""";

    public const string InvalidTypeBoolDivision =
        "Number s = true / false;";

    // Both declarations bind the same name in the same scope.
    public const string InvalidTypeDuplicateVar =
        "Number x = 5;\nNumber x = 10;";

    // Reference to an undeclared variable.
    public const string InvalidTypeUndeclaredVar =
        "Number x = undeclared;";

    // if-statement with a non-bool condition.
    public const string InvalidTypeIfNonBoolCondition =
        "Number n = 5;\nif (n) then { Bool c = true; }";

    // ── Boolean operator precedence ──────────────────────────────────────────
    //
    // The grammar binds AND tighter than OR: LogicalAndExp is nested inside
    // LogicalOrExp. So "true and false or true" must parse as
    //   OR(AND(BoolV(true), BoolV(false)), BoolV(true))
    // — not as AND(BoolV(true), OR(BoolV(false), BoolV(true))).
    public const string ValidAndOrPrecedence =
        "Bool result = true and false or true;";

    // ── Reservation combinator programs ──────────────────────────────────────
    //
    // The grammar overloads "and"/"or" between Bool operands AND between
    // Reservation operands; "seq" is reservation-only.
    // TypeChecker rules (HandleBinary):
    //   AND/OR: (BoolT, BoolT) | (ReservationT, ReservationT)
    //   SEQ:    (ReservationT, ReservationT)

    // Two reserves combined with "seq" → composite (best-effort sequenced) reservation.
    public const string ValidReserveSeqReserve =
        "category Room;\nRoom myRoom {}\n" +
        "Reservation res = reserve myRoom from 15/03-2026 to 16/03-2026 " +
        "seq reserve myRoom from 17/03-2026 to 18/03-2026;";

    // Two reserves combined with "or" → choice reservation (either may satisfy).
    public const string ValidReserveOrReserve =
        "category Room;\nRoom myRoom {}\n" +
        "Reservation res = reserve myRoom from 15/03-2026 to 16/03-2026 " +
        "or reserve myRoom from 17/03-2026 to 18/03-2026;";

    // Two reserves combined with "and" → both-required reservation.
    public const string ValidReserveAndReserve =
        "category Room;\nRoom myRoom {}\n" +
        "Reservation res = reserve myRoom from 15/03-2026 to 16/03-2026 " +
        "and reserve myRoom from 17/03-2026 to 18/03-2026;";

    // Type error: SEQ requires (ReservationT, ReservationT); (BoolT, BoolT) must be rejected.
    public const string InvalidSeqOnBools =
        "Reservation res = true seq false;";

    // ── Recurring reservation programs ───────────────────────────────────────
    //
    // Recurrence grammar (tail of QueryExp):
    //   "recurring" ("strict" | "flexible") "every" Exp ("until" DateTime | "for" Duration)
    // TypeChecker.RecurrenceIsWellTyped accepts:
    //   (DurationT, DateTimeT) → "every D until DT"
    //   (DurationT, DurationT) → "every D for D"
    //   anything else → error

    // "every 1 week until 30/06-2026" — strict mode, until-DateTime form.
    public const string ValidRecurringStrictUntil =
        "category Room;\nRoom myRoom {}\n" +
        "Reservation res = reserve myRoom from 15/03-2026 to 16/03-2026 " +
        "recurring strict every 1 week until 30/06-2026;";

    // "every 1 week for 4 weeks" — flexible mode, for-Duration form.
    public const string ValidRecurringFlexibleFor =
        "category Room;\nRoom myRoom {}\n" +
        "Reservation res = reserve myRoom from 15/03-2026 to 16/03-2026 " +
        "recurring flexible every 1 week for 4 weeks;";

    // Runtime-deterministic recurrence: "every 1 week for 3 weeks" anchored at
    // 15/03-2026 → start dates 15/03, 22/03, 29/03, 5/04. Each atom is one day
    // long (16/03 − 15/03). Used by the interpreter test that asserts the
    // expansion produces exactly N atomic reservations with the right dates.
    public const string RecurringWeeklyForThreeWeeksProgram =
        "category Room;\nRoom myRoom {}\n" +
        "Reservation res = reserve myRoom from 15/03-2026 to 16/03-2026 " +
        "recurring strict every 1 week for 3 weeks;";

    // Type error: "every 5" — 5 has type Number, recurrence requires Duration.
    public const string InvalidRecurringNumberInterval =
        "category Room;\nRoom myRoom {}\n" +
        "Reservation res = reserve myRoom from 15/03-2026 to 16/03-2026 " +
        "recurring strict every 5 until 30/06-2026;";

    // ── Malformed reserve/check syntax ───────────────────────────────────────
    //
    // Domain-specific grammar errors — distinct from the generic
    // "missing semicolon / unclosed paren" cases above.

    // "reserve myRoom 15/03-2026 …" — the required "from" keyword is missing
    // before the start-date expression.
    public const string InvalidSyntaxReserveMissingFrom =
        "category Room;\nRoom myRoom {}\n" +
        "Reservation res = reserve myRoom 15/03-2026 to 16/03-2026;";

    // "check myRoom from 15/03-2026" — Time non-terminal needs either
    // "to DateTime" or "for Duration" after the start; neither is present.
    public const string InvalidSyntaxCheckMissingToOrFor =
        "category Room;\nRoom myRoom {}\ncheck myRoom from 15/03-2026;";

    // ── Runtime-focused programs used only by InterpreterTests ───────────────
    //
    // These programs are crafted so that the post-execution state is
    // deterministic and observable (resource count, atom count, selected
    // resource identity, conflict outcome). They typecheck successfully and
    // are designed to drive the full Parse → TypeCheck → Interpret pipeline.

    // Two non-overlapping reserves of the same resource combined with "seq".
    // After execution res must be a composite ReservationVal containing
    // exactly two atoms: 15/03 → 16/03 and 17/03 → 18/03.
    public const string CompositeSeqReservationProgram =
        "category Room;\nRoom myRoom {}\n" +
        "Reservation res = reserve myRoom from 15/03-2026 to 16/03-2026 " +
        "seq reserve myRoom from 17/03-2026 to 18/03-2026;";

    // Two rooms with distinct beds counts; reserve "1 Room r where (r.beds == 2)".
    // Only roomTwoBeds satisfies the predicate, so the resulting ReservationVal
    // must contain a single atom whose Resources is exactly [roomTwoBeds].
    public const string WhereClauseFilteringProgram =
        "category Room;\n" +
        "Room roomTwoBeds { Number beds = 2; }\n" +
        "Room roomFourBeds { Number beds = 4; }\n" +
        "Reservation res = reserve 1 Room r from 15/03-2026 to 16/03-2026 where (r.beds == 2);";

    // Two reserves of the same resource in the SAME interval.
    // The first must succeed (Failed() == false) and the second must fail
    // (Failed() == true) because IsAvailable rejects the overlap.
    public const string ConflictingReservationProgram =
        "category Room;\nRoom myRoom {}\n" +
        "Reservation first = reserve myRoom from 15/03-2026 to 16/03-2026;\n" +
        "Reservation second = reserve myRoom from 15/03-2026 to 16/03-2026;";

    // Move a resource from Room into the Suite subcategory, then reserve
    // "1 Suite" for the same interval. After execution res must be a single
    // atom whose Resources contains exactly myRoom — proving the moved
    // resource is reachable through the new category's subtree.
    public const string MoveThenReserveInNewCategoryProgram =
        "category Room;\ncategory Suite is a Room;\n" +
        "Room myRoom {}\n" +
        "move myRoom to Suite;\n" +
        "Reservation res = reserve 1 Suite from 15/03-2026 to 16/03-2026;";

    // A then-branch declares a local Number x. After execution the outer
    // scope must NOT see x — Interpreter.HandleIf opens envV.NewScope() for
    // the branch body, so the binding lives only in that child scope and
    // Lookup("x") on the outer envV must throw.
    public const string IfScopeIsolationProgram =
        "if (true) then { Number x = 5; }";

    // A reservation for 15/03 → 17/03 followed by an overlapping availability
    // check for 16/03 → 18/03. The interval 16/03 → 17/03 is already held,
    // so ReservationRegistry.IsAvailable must return false and the
    // Availability statement must print "Availability check failed: ...".
    public const string AvailabilityWithConflictProgram =
        "category Room;\nRoom myRoom {}\n" +
        "Reservation booking = reserve myRoom from 15/03-2026 to 17/03-2026;\n" +
        "check myRoom from 16/03-2026 to 18/03-2026;";

    // ── End-to-end acceptance scenario ───────────────────────────────────────
    //
    // A single program that exercises every front-end module cooperatively:
    //   category + subcategory declarations
    //   two ResourceDecls with property bodies
    //   move (relocates roomB into the subcategory)
    //   named-resource reserve (binds r1 to a live reservation on roomA)
    //   category + alias + where reserve over the parent category — this is
    //     the load-bearing part: GetSubCategories("Room") must include Suite,
    //     so the moved roomB is reachable through the parent's subtree, and
    //     the where predicate must filter the Cartesian product down to
    //     exactly the resource whose beds property equals 4.
    //   cancel of r1
    //
    // The post-execution state pins all of those behaviours simultaneously:
    //   r1.Failed() == true                      (cancel cleared its atoms)
    //   r2.Failed() == false                     (filter found a match)
    //   r2 has exactly one atom on 17/03 → 18/03
    //   r2's atom reserves exactly roomB         (filter picked beds == 4)
    //   roomA.CategoryId remains "Room"          (move only touched roomB)
    //   roomB.CategoryId is now "Suite"          (move propagated)
    public const string EndToEndScenarioProgram =
        "category Room;\n" +
        "category Suite is a Room;\n" +
        "Room roomA { Number beds = 2; }\n" +
        "Room roomB { Number beds = 4; }\n" +
        "move roomB to Suite;\n" +
        "Reservation r1 = reserve roomA from 15/03-2026 to 16/03-2026;\n" +
        "Reservation r2 = reserve 1 Room r from 17/03-2026 to 18/03-2026 where (r.beds == 4);\n" +
        "cancel r1;";
}
