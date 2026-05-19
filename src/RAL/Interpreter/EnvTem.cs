using RAL.AST;
namespace RAL.Interpreter;

/// <summary> Template Environment, supporting lookup and bind of a symbol, i.e. templateId, -> encapsulation of param names & body. 
/// Return values not supported. </summary>
public class EnvTem {

    //Datastructure "template table"
    private readonly Dictionary<string, tAttribute > tTable = new();

    public void Bind(string templateId, List<string> parameterNames, Stmt body, EnvV declarationScope) {

        tTable.Add(templateId, new tAttribute(parameterNames, body, declarationScope));        
    }

    internal tAttribute Lookup(string templateId) {
        return tTable[templateId];        
    }
}

internal record class tAttribute(
        List<string> ParameterNames,
        Stmt Body,
        EnvV DeclarationScope
    );
