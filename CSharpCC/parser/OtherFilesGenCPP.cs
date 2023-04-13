// Copyright 2012 Google Inc. All Rights Reserved.
// Author: sreeni@google.com (Sreeni Viswanadha)

/* Copyright (c) 2006, Sun Microsystems, Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 *     * Redistributions of source code must retain the above copyright notice,
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Sun Microsystems, Inc. nor the names of its
 *       contributors may be used to endorse or promote products derived from
 *       this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
 * THE POSSIBILITY OF SUCH DAMAGE.
 */
using org.javacc.jjtree;

namespace org.javacc.parser;


/**
 * Generates the Constants file.
 */
public class OtherFilesGenCPP : JavaCCGlobals
{

    static public void start()
    {

        Token t = null;
        if (JavaCCErrors.GetErrorCount() != 0) throw new MetaParseException();

        CPPFiles.GenJavaCCDefs();
        CPPFiles.GenCharStream();
        CPPFiles.GenToken();  // TODO(theov): issued twice??
        CPPFiles.GenTokenManager();
        CPPFiles.GenTokenMgrError();
        CPPFiles.GenParseException();
        CPPFiles.GenErrorHandler();

        try
        {
            ostr = new StreamWriter(
                System.IO.Path.Combine(Options.getOutputDirectory(), CuName + "Constants.h"));
        }
        catch (IOException e)
        {
            JavaCCErrors.SemanticError("Could not open file " + CuName + "Constants.h for writing.");
            throw new Error();
        }

        List<string> tn = new(ToolNames);
        tn.Add(ToolName);
        ostr.WriteLine("/* " + GetIdString(tn, CuName + "Constants.java") + " */");

        if (cu_to_insertion_point_1.Count != 0 &&
            ((Token)cu_to_insertion_point_1[0]).kind == PACKAGE
           )
        {
            for (int i = 1; i < cu_to_insertion_point_1.Count; i++)
            {
                if (((Token)cu_to_insertion_point_1[i]).kind == SEMICOLON)
                {
                    PrintTokenSetup((Token)(cu_to_insertion_point_1[0]));
                    for (int j = 0; j <= i; j++)
                    {
                        t = (Token)(cu_to_insertion_point_1[j]);
                        PrintToken(t, ostr);
                    }
                    PrintTrailingComments(t, ostr);
                    ostr.WriteLine("");
                    ostr.WriteLine("");
                    break;
                }
            }
        }
        ostr.WriteLine("");
        ostr.WriteLine("/**");
        ostr.WriteLine(" * Token literal values and constants.");
        ostr.WriteLine(" * Generated by org.javacc.parser.OtherFilesGenCPP#start()");
        ostr.WriteLine(" */");

        string define = (CuName + "Constants_h").ToUpper();
        ostr.WriteLine("#ifndef " + define);
        ostr.WriteLine("#define " + define);
        ostr.WriteLine("#include \"JavaCC.h\"");
        ostr.WriteLine("");
        if (Options.stringValue(Options.USEROPTION__CPP_NAMESPACE).Length > 0)
        {
            ostr.WriteLine("namespace " + Options.stringValue("NAMESPACE_OPEN"));
        }

        RegularExpression re; 
        string constPrefix = "const"; 
        ostr.WriteLine("  /** End of File. */");
        ostr.WriteLine(constPrefix + "  int _EOF = 0;");
        for (Iterator<RegularExpression> it = ordered_named_tokens.iterator(); it.hasNext();)
        {
            re = it.next();
            ostr.WriteLine("  /** RegularExpression Id. */");
            ostr.WriteLine(constPrefix + "  int " + re.label + " = " + re.ordinal + ";");
        }
        ostr.WriteLine("");

        if (!Options.getUserTokenManager() && Options.getBuildTokenManager())
        {
            for (int i = 0; i < MainParser.LexGenerator.lexStateName.Length; i++)
            {
                ostr.WriteLine("  /** Lexical state. */");
                ostr.WriteLine(constPrefix + "  int " + LexGen.lexStateName[i] + " = " + i + ";");
            }
            ostr.WriteLine("");
        }
        ostr.WriteLine("  /** Literal token values. */");
        int cnt = 0;
        ostr.WriteLine("  static const JJChar tokenImage_arr_" + cnt + "[] = ");
        printCharArray(ostr, "<EOF>");
        ostr.WriteLine(";");

        for (Iterator<TokenProduction> it = rexprlist.iterator(); it.hasNext();)
        {
            TokenProduction tp = it.next();
            List<RegExprSpec> respecs = tp.respecs;
            for (Iterator<RegExprSpec> it2 = respecs.iterator(); it2.hasNext();)
            {
                RegExprSpec res = it2.next();
                re = res.rexp;
                ostr.WriteLine("  static const JJChar tokenImage_arr_" + ++cnt + "[] = ");
                if (re is RStringLiteral)
                {
                    printCharArray(ostr, "\"" + ((RStringLiteral)re).image + "\"");
                }
                else if (!re.label == (""))
                {
                    printCharArray(ostr, "\"<" + re.label + ">\"");
                }
                else
                {
                    if (re.tpContext.kind == TokenProduction.TOKEN)
                    {
                        JavaCCErrors.Warning(re, "Consider giving this non-string token a label for better error reporting.");
                    }
                    printCharArray(ostr, "\"<token of kind " + re.ordinal + ">\"");
                }
                ostr.WriteLine(";");
            }
        }

        ostr.WriteLine("  static const JJChar* const tokenImage[] = {");
        for (int i = 0; i <= cnt; i++)
        {
            ostr.WriteLine("tokenImage_arr_" + i + ", ");
        }
        ostr.WriteLine("  };");
        ostr.WriteLine("");
        if (Options.stringValue(Options.USEROPTION__CPP_NAMESPACE).Length > 0)
        {
            ostr.WriteLine(Options.stringValue("NAMESPACE_CLOSE"));
        }
        ostr.WriteLine("#endif");
        ostr.Close();

    }

    // Used by the CPP code generatror
    public static void printCharArray(TextWriter ostr, string s)
    {
        ostr.Write("{");
        for (int i = 0; i < s.Length; i++)
        {
            ostr.Write("0x" + int.toHexString((int)s[i]) + ", ");
        }
        ostr.Write("0}");
    }

    static private TextWriter ostr;

    public static void ReInit()
    {
        ostr = null;
    }

}
