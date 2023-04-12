/* Generated By:JavaCC: Do not edit this line. ConditionParserConstants.java */
namespace org.javacc.utils;


/**
 * Token literal values and constants.
 * Generated by org.javacc.parser.OtherFilesGen#start()
 */
public interface ConditionParserConstants
{

    /** End of File. */
    public const int EOF = 0;
    /** RegularExpression Id. */
    public const int SINGLE_LINE_COMMENT = 9;
    /** RegularExpression Id. */
    public const int FORMAL_COMMENT = 10;
    /** RegularExpression Id. */
    public const int MULTI_LINE_COMMENT = 11;
    /** RegularExpression Id. */
    public const int LPAREN = 13;
    /** RegularExpression Id. */
    public const int RPAREN = 14;
    /** RegularExpression Id. */
    public const int BANG = 15;
    /** RegularExpression Id. */
    public const int SC_OR = 16;
    /** RegularExpression Id. */
    public const int SC_AND = 17;
    /** RegularExpression Id. */
    public const int TRUE = 19;
    /** RegularExpression Id. */
    public const int FALSE = 20;
    /** RegularExpression Id. */
    public const int IDENTIFIER = 21;
    /** RegularExpression Id. */
    public const int LETTER = 22;
    /** RegularExpression Id. */
    public const int PART_LETTER = 23;

    /** Lexical state. */
    public const int DEFAULT = 0;
    /** Lexical state. */
    public const int IN_SINGLE_LINE_COMMENT = 1;
    /** Lexical state. */
    public const int IN_FORMAL_COMMENT = 2;
    /** Lexical state. */
    public const int IN_MULTI_LINE_COMMENT = 3;

    /** Literal token values. */
    public static readonly string[] tokenImage = {
    "<EOF>",
    "\" \"",
    "\"\\t\"",
    "\"\\n\"",
    "\"\\r\"",
    "\"\\f\"",
    "\"//\"",
    "<token of kind 7>",
    "\"/*\"",
    "<SINGLE_LINE_COMMENT>",
    "\"*/\"",
    "\"*/\"",
    "<token of kind 12>",
    "\"(\"",
    "\")\"",
    "\"!\"",
    "\"||\"",
    "\"&&\"",
    "\"~\"",
    "\"true\"",
    "\"false\"",
    "<IDENTIFIER>",
    "<LETTER>",
    "<PART_LETTER>",
  };

}
