/* Generated By:JJTree: Do not edit this line. ASTBNFChoice.java Version 4.3 */
/* JavaCCOptions:MULTI=true,NODE_USES_PARSER=false,VISITOR=true,TRACK_TOKENS=false,NODE_PREFIX=AST,NODE_EXTENDS=,NODE_FACTORY=,SUPPORT_CLASS_VISIBILITY_PUBLIC=true */
namespace org.javacc.jjtree;

public class ASTBNFChoice : JJTreeNode
{
    public ASTBNFChoice(int id) : base(id)
    {
    }

    public ASTBNFChoice(JJTreeParser p, int id) : base(p, id)
    {
    }


    /** Accept the visitor. **/
    public Object jjtAccept(JJTreeParserVisitor visitor, Object data)
    {
        return visitor.visit(this, data);
    }
}
/* JavaCC - OriginalChecksum=c6136f2093e7bd7093fb5ebfc93097da (do not edit this line) */
