/* Generated By:JJTree: Do not edit this line. ASTBNFAssignment.java Version 4.3 */
/* JavaCCOptions:MULTI=true,NODE_USES_PARSER=false,VISITOR=true,TRACK_TOKENS=false,NODE_PREFIX=AST,NODE_EXTENDS=,NODE_FACTORY=,SUPPORT_CLASS_VISIBILITY_PUBLIC=true */
namespace org.javacc.jjtree;

public
class ASTBNFAssignment : JJTreeNode
{
    public ASTBNFAssignment(int id) : base(id)
    {
    }

    public ASTBNFAssignment(JJTreeParser p, int id) : base(p, id)
    {
    }


    /** Accept the visitor. **/
    public Object jjtAccept(JJTreeParserVisitor visitor, Object data)
    {
        return visitor.visit(this, data);
    }
}
/* JavaCC - OriginalChecksum=abf306a83b75e794e7006b68a9512e78 (do not edit this line) */
