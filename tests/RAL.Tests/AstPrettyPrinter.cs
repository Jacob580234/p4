using System.Globalization;
using System.Text;
using RAL.AST;

namespace RAL.Tests;

/*
 * Test-side pretty-printer for the RAL AST.
 *
 * Lives in the tests folder — production AST classes are untouched.
 *
 * Purpose: enable parse → pretty-print → reparse round-trip tests. Round-trip
 * stability is the only correctness criterion: Print must produce text that
 * the parser accepts and that parses back to the same AST shape (modulo line
 * numbers). The output is canonical RAL surface syntax — it need NOT match
 * the original source token-for-token (e.g. "1 week" round-trips as "7 days"
 * because the AST stores a TimeSpan that has lost the original unit).
 *
 * Coverage: every Stmt and Exp subtype that the test programs exercise.
 * If a round-trip test introduces a node not handled, the switch's default
 * branch throws — the failure points at exactly what to add.
 *
 * Notes on choices:
 *   - BinaryOperations are wrapped in explicit parens to preserve precedence
 *     unambiguously across reparse. The parser accepts redundant parens.
 *   - VarDecl + Assignment for the same identifier is fused into the
 *     combined "Type id = expr;" form. That form is required inside
 *     ResourceDecl property lists and is the most readable elsewhere.
 *   - TimeSpec and RecurrenceSpec both encode the surface keyword in the AST
 *     subtype (TimeSpecTo / TimeSpecFor and RecurrenceUntil / RecurrenceFor),
 *     but the printer recovers the keyword two different ways:
 *       - RecurrenceSpec dispatches on AST subtype: `rs.Time is RecurrenceFor`
 *         picks "for", otherwise "until". Robust for any EndMarker value.
 *       - TimeSpec dispatches on the EndMarker's runtime value type:
 *         DurationV → "for", otherwise "to". Works for literal-driven inputs
 *         only — a non-literal Exp in EndMarker would fall through to "to"
 *         regardless of the original surface keyword. Inconsistent with the
 *         RecurrenceSpec branch; left as-is until a test forces the change.
 */
static class AstPrettyPrinter
{
    public static string Print(Stmt stmt)
    {
        if (stmt is Composite cc && TryFuseVarDeclAssignment(cc, out var fused))
            return fused;
        return stmt switch
        {
            Composite c     => Print(c.Stmt1) + "\n" + Print(c.Stmt2),
            VarDecl vd      => $"{vd.Type} {vd.Identifier};",
            ResourceDecl r  => r.PropertyList.Count == 0
                ? $"{r.Type} {r.Identifier} {{}}"
                : $"{r.Type} {r.Identifier} {{ {PrintPropertyList(r.PropertyList)} }}",
            CategoryDecl cd => cd.ParentId is null
                ? $"category {cd.CategoryId};"
                : $"category {cd.CategoryId} is a {cd.ParentId};",
            TemplateDecl td => $"template {td.TemplateId}({PrintParams(td.ParamList)}) {{ {Print(td.TemplateBody)} }}",
            Move m          => $"move {m.ResourceId} to {m.Type.Category};",
            Cancel cn       => $"cancel {Print(cn.Reservation)};",
            If i            => $"if ({Print(i.Condition)}) then {{ {Print(i.ThenBody)} }} else {{ {Print(i.ElseBody)} }}",
            ExpStmt es      => $"{Print(es.Expression)};",
            Availability av => $"check {PrintQuery(av.Query)};",
            TemplateCall tc => $"use {tc.TemplateId}({string.Join(", ", tc.ArgList.Select(Print))});",
            Skip _          => "",
            _ => throw new NotSupportedException($"AstPrettyPrinter: Stmt subtype {stmt.GetType().Name} not handled"),
        };
    }

    public static string Print(Exp exp) => exp switch
    {
        BoolV b            => b.Value ? "true" : "false",
        StringV s          => $"\"{s.Value}\"",
        NumberV n          => PrintNumber(n.Value),
        DateTimeV dt       => PrintDateTime(dt.Value),
        DurationV d        => PrintDuration(d.Value),
        Reference r        => r.PropertyId is null ? r.VariableId : $"{r.VariableId}.{r.PropertyId}",
        Assignment a       => $"{Print(a.Variable)} = {Print(a.Expression)}",
        BinaryOperation bo => $"({Print(bo.LeftExpression)} {OpName(bo.Operator)} {Print(bo.RightExpression)})",
        UnaryOperation uo  => PrintUnary(uo),
        Reserve r          => $"reserve {PrintQuery(r.Query)}",
        Reschedule rs      => $"reschedule {Print(rs.Reservation)} {PrintTimeSpec(rs.NewTimeInterval)}",
        _ => throw new NotSupportedException($"AstPrettyPrinter: Exp subtype {exp.GetType().Name} not handled"),
    };

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool TryFuseVarDeclAssignment(Composite c, out string fused)
    {
        if (c.Stmt1 is VarDecl vd
            && c.Stmt2 is ExpStmt es
            && es.Expression is Assignment a
            && a.Variable.PropertyId is null
            && a.Variable.VariableId == vd.Identifier)
        {
            fused = $"{vd.Type} {vd.Identifier} = {Print(a.Expression)};";
            return true;
        }
        fused = "";
        return false;
    }

    private static string PrintPropertyList(List<Stmt> list) =>
        string.Join(" ", list.Select(Print));

    private static string PrintParams(List<VarDecl> list) =>
        string.Join(", ", list.Select(vd => $"{vd.Type} {vd.Identifier}"));

    private static string PrintQuery(QueryData q)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(", ", q.ResourceSpecs.Select(PrintResourceSpec)));
        sb.Append(' ').Append(PrintTimeSpec(q.Interval));
        if (q.Condition is not null)
            sb.Append(" where (").Append(Print(q.Condition)).Append(')');
        if (q.Recurrence is not null)
            sb.Append(' ').Append(PrintRecurrence(q.Recurrence));
        return sb.ToString();
    }

    private static string PrintResourceSpec(ResourceSpec rs) => rs switch
    {
        // a*rc id forms:
        //   id-only           → "{id}"
        //   qty + category    → "{qty} {category}"
        //   qty + cat + alias → "{qty} {category} {alias}"
        ResourceInstanceSpec ri      => ri.ResourceId,
        CategorySpecWithBinding cwb  => $"{Print(cwb.Quantity)} {cwb.CategoryId} {cwb.LocalBindingId}",
        CategorySpec cs              => $"{Print(cs.Quantity)} {cs.CategoryId}",
        _ => throw new NotSupportedException($"ResourceSpec {rs.GetType().Name}"),
    };

    private static string PrintTimeSpec(TimeSpec ts)
    {
        var endKw = ts.EndMarker is DurationV ? "for" : "to";
        return $"from {Print(ts.Start)} {endKw} {Print(ts.EndMarker)}";
    }

    private static string PrintRecurrence(RecurrenceSpec rs)
    {
        var mode = rs.Mode switch
        {
            RecurrenceMode.STRICT   => "strict",
            RecurrenceMode.FLEXIBLE => "flexible",
            _ => throw new NotSupportedException($"RecurrenceMode {rs.Mode}"),
        };
        var endKw = rs.Time is RecurrenceFor ? "for" : "until";
        return $"recurring {mode} every {Print(rs.Time.Every)} {endKw} {Print(rs.Time.EndMarker)}";
    }

    private static string PrintUnary(UnaryOperation uo) => uo.Operator switch
    {
        UnaryOperator.NOT => $"not({Print(uo.Expression)})",
        UnaryOperator.NEG => $"-({Print(uo.Expression)})",
        _ => throw new NotSupportedException($"UnaryOperator {uo.Operator}"),
    };

    private static string OpName(BinaryOperator op) => op switch
    {
        BinaryOperator.ADD  => "+",
        BinaryOperator.SUB  => "-",
        BinaryOperator.MUL  => "*",
        BinaryOperator.DIV  => "/",
        BinaryOperator.EQ   => "==",
        BinaryOperator.NEQ  => "!=",
        BinaryOperator.LT   => "<",
        BinaryOperator.GT   => ">",
        BinaryOperator.LTEQ => "<=",
        BinaryOperator.GTEQ => ">=",
        BinaryOperator.AND  => "and",
        BinaryOperator.OR   => "or",
        BinaryOperator.SEQ  => "seq",
        _ => throw new NotSupportedException($"BinaryOperator {op}"),
    };

    private static string PrintNumber(float n)
    {
        if (n == MathF.Truncate(n) && !float.IsInfinity(n))
            return ((long)n).ToString(CultureInfo.InvariantCulture);
        return n.ToString(CultureInfo.InvariantCulture);
    }

    private static string PrintDateTime(DateTime dt)
    {
        var date = dt.ToString("dd/MM-yyyy", CultureInfo.InvariantCulture);
        if (dt.Hour == 0 && dt.Minute == 0 && dt.Second == 0)
            return date;
        return $"{date} {dt:HH:mm}";
    }

    private static string PrintDuration(TimeSpan ts)
    {
        if (ts == TimeSpan.Zero) return "0 minutes";
        var parts = new List<string>();
        int days = (int)ts.TotalDays;
        int hours = ts.Hours;
        int minutes = ts.Minutes;
        if (days > 0)    parts.Add($"{days} {(days == 1 ? "day" : "days")}");
        if (hours > 0)   parts.Add($"{hours} {(hours == 1 ? "hour" : "hours")}");
        if (minutes > 0) parts.Add($"{minutes} {(minutes == 1 ? "minute" : "minutes")}");
        return string.Join(" ", parts);
    }
}
