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

namespace org.javacc.parser;

public class Semanticize : JavaCCGlobals
{

    public static List<List<RegExprSpec>> removeList = new();
    public static List<RegExprSpec> itemList = new();

    public static void PrepareToRemove(List<RegExprSpec> vec, RegExprSpec item) 
    {
        removeList.Add(vec);
        itemList.Add(item);
    }

    public static void RemovePreparedItems()
    {
        for (int i = 0; i < removeList.Count; i++)
        {
            var list = removeList[i];
            list.Remove(itemList[i]);
        }
        removeList.Clear();
        itemList.Clear(); 
    }

    public static void Start()
    {

        if (JavaCCErrors.GetErrorCount() != 0) throw new MetaParseException();

        if (Options.GetLookahead() > 1 && !Options.GetForceLaCheck() && Options.GetSanityCheck())
        {
            JavaCCErrors.Warning("Lookahead adequacy checking not being performed since option LOOKAHEAD " +
                    "is more than 1.  Set option FORCE_LA_CHECK to true to force checking.");
        }

        /*
         * The following walks the entire parse tree to convert all LOOKAHEAD's
         * that are not at choice points (but at beginning of sequences) and converts
         * them to trivial choices.  This way, their semantic lookahead specification
         * can be evaluated during other lookahead evaluations.
         */
        foreach(var np in bnfproductions)
        {
            ExpansionTreeWalker.PostOrderWalk(np.GetExpansion(),
                                              new LookaheadFixer());
        }

        /*
         * The following loop populates "production_table"
         */
        foreach(var p in bnfproductions)
        {
            if (production_table.ContainsKey(p.GetLhs()))
            {
                JavaCCErrors.SemanticError(p, p.GetLhs() + " occurs on the left hand side of more than one production.");
            }
            production_table.Add(p.GetLhs(), p);
        }

        /*
         * The following walks the entire parse tree to make sure that all
         * non-terminals on RHS's are defined on the LHS.
         */
        foreach(var np in bnfproductions)
        {
            ExpansionTreeWalker.PreOrderWalk(np.GetExpansion(), new ProductionDefinedChecker());
        }

        /*
         * The following loop ensures that all target lexical states are
         * defined.  Also piggybacking on this loop is the detection of
         * <EOF> and <name> in token productions.  After reporting an
         * error, these entries are removed.  Also checked are definitions
         * on inline private regular expressions.
         * This loop works slightly differently when USER_TOKEN_MANAGER
         * is set to true.  In this case, <name> occurrences are OK, while
         * regular expression specs generate a warning.
         */
        foreach(var tp in rexprlist)
        { 
            List<RegExprSpec> respecs = tp.respecs;
            foreach(var res in respecs)
            {
                if (res.nextState != null)
                {
                    if (!lexstate_S2I.TryGetValue(res.nextState,out var _))
                    {
                        JavaCCErrors.SemanticError(res.nsTok, "Lexical state \"" + res.nextState +
                                                               "\" has not been defined.");
                    }
                }
                if (res.rexp is REndOfFile)
                {
                    //JavaCCErrors.semantic_error(res.rexp, "Badly placed <EOF>.");
                    if (tp.lexStates != null)
                        JavaCCErrors.SemanticError(res.rexp, "EOF action/state change must be specified for all states, " +
                                "i.e., <*>TOKEN:.");
                    if (tp.kind != TokenProduction.TOKEN)
                        JavaCCErrors.SemanticError(res.rexp, "EOF action/state change can be specified only in a " +
                                "TOKEN specification.");
                    if (nextStateForEof != null || actForEof != null)
                        JavaCCErrors.SemanticError(res.rexp, "Duplicate action/state change specification for <EOF>.");
                    actForEof = res.act;
                    nextStateForEof = res.nextState;
                    PrepareToRemove(respecs, res);
                }
                else if (tp.isExplicit && Options.GetUserTokenManager())
                {
                    JavaCCErrors.Warning(res.rexp, "Ignoring regular expression specification since " +
                            "option USER_TOKEN_MANAGER has been set to true.");
                }
                else if (tp.isExplicit && !Options.GetUserTokenManager() && res.rexp is RJustName)
                {
                    JavaCCErrors.Warning(res.rexp, "Ignoring free-standing regular expression reference.  " +
                            "If you really want this, you must give it a different label as <NEWLABEL:<"
                            + res.rexp.label + ">>.");
                    PrepareToRemove(respecs, res);
                }
                else if (!tp.isExplicit && res.rexp.private_rexp)
                {
                    JavaCCErrors.SemanticError(res.rexp, "Private (#) regular expression cannot be defined within " +
                            "grammar productions.");
                }
            }
        }

        RemovePreparedItems();

        /*
         * The following loop inserts all names of regular expressions into
         * "named_tokens_table" and "ordered_named_tokens".
         * Duplications are flagged as errors.
         */
        foreach(var tp in rexprlist)
        {
            List<RegExprSpec> respecs = tp.respecs;
            foreach(var res in respecs)
            {
                if (res.rexp is not RJustName && res.rexp.label != "")
                {
                    string s = res.rexp.label;
                    var b = named_tokens_table.ContainsKey(s);
                    named_tokens_table.Add(s, res.rexp);
                    if (b)
                    {
                        JavaCCErrors.SemanticError(res.rexp, "Multiply defined lexical token name \"" + s + "\".");
                    }
                    else
                    {
                        ordered_named_tokens.Add(res.rexp);
                    }
                    if (!lexstate_S2I.TryGetValue(s,out var _))
                    {
                        JavaCCErrors.SemanticError(res.rexp, "Lexical token name \"" + s + "\" is the same as " +
                                "that of a lexical state.");
                    }
                }
            }
        }

        /*
         * The following code merges multiple uses of the same string in the same
         * lexical state and produces error messages when there are multiple
         * explicit occurrences (outside the BNF) of the string in the same
         * lexical state, or when within BNF occurrences of a string are duplicates
         * of those that occur as non-TOKEN's (SKIP, MORE, SPECIAL_TOKEN) or private
         * regular expressions.  While doing this, this code also numbers all
         * regular expressions (by setting their ordinal values), and populates the
         * table "names_of_tokens".
         */

        tokenCount = 1;
        foreach(var tp in rexprlist)
        {
            List<RegExprSpec> respecs = tp.respecs;
            if (tp.lexStates == null)
            {
                tp.lexStates = new String[lexstate_I2S.Count];
                int i = 0;
                foreach(var v in lexstate_I2S.Values)
                {
                    tp.lexStates[i++] = v;
                }
            }
            var table = new Dictionary<string, Dictionary<string, RegularExpression>>[tp.lexStates.Length];
            for (int i = 0; i < tp.lexStates.Length; i++)
            {
                simple_tokens_table.TryGetValue(tp.lexStates[i], out table[i]);
            }
            foreach(var res in respecs)
            {
                if (res.rexp is RStringLiteral sl)
                {
                    // This loop performs the checks and actions with respect to each lexical state.
                    for (int i = 0; i < table.Length; i++)
                    {
                        // Get table of all case variants of "sl.image" into table2.
                        //Dictionary table2 = (Dictionary)(table[i].get(sl.image.ToUpper()));
                        if (table[i].TryGetValue(sl.image.ToUpper(),out var table2))
                        {
                            // There are no case variants of "sl.image" earlier than the current one.
                            // So go ahead and insert this item.
                            if (sl.ordinal == 0)
                            {
                                sl.ordinal = tokenCount++;
                            }
                            table2 = new ();
                            table2.Add(sl.image, sl);
                            table[i].Add(sl.image.ToUpper(), table2);
                        }
                        else if (HasIgnoreCase(table2, sl.image))
                        { // hasIgnoreCase sets "other" if it is found.
                          // Since IGNORE_CASE version exists, current one is useless and bad.
                            if (!sl.tpContext.isExplicit)
                            {
                                // inline BNF string is used earlier with an IGNORE_CASE.
                                JavaCCErrors.SemanticError(sl, "String \"" + sl.image + "\" can never be matched " +
                                        "due to presence of more general (IGNORE_CASE) regular expression " +
                                        "at line " + other.GetLine() + ", column " + other.GetColumn() + ".");
                            }
                            else
                            {
                                // give the standard error message.
                                JavaCCErrors.SemanticError(sl, "Duplicate definition of string token \"" + sl.image + "\" " +
                                        "can never be matched.");
                            }
                        }
                        else if (sl.tpContext.ignoreCase)
                        {
                            // This has to be explicit.  A warning needs to be given with respect
                            // to all previous strings.
                            String pos = ""; int count = 0;
                            foreach(RegularExpression rexp in table2.Values)
                            {
                                if (count != 0) pos += ",";
                                pos += " line " + rexp.GetLine();
                                count++;
                            }
                            if (count == 1)
                            {
                                JavaCCErrors.Warning(sl, "String with IGNORE_CASE is partially superseded by string at" + pos + ".");
                            }
                            else
                            {
                                JavaCCErrors.Warning(sl, "String with IGNORE_CASE is partially superseded by strings at" + pos + ".");
                            }
                            // This entry is legitimate.  So insert it.
                            if (sl.ordinal == 0)
                            {
                                sl.ordinal = tokenCount++;
                            }
                            table2.Add(sl.image, sl);
                            // The above "put" may override an existing entry (that is not IGNORE_CASE) and that's
                            // the desired behavior. 
                        }
                        else
                        {
                            // The rest of the cases do not involve IGNORE_CASE.
                            //RegularExpression re = (RegularExpression)table2.get(sl.image);
                            if (table2.TryGetValue(sl.image,out var re))
                            {
                                if (sl.ordinal == 0)
                                {
                                    sl.ordinal = tokenCount++;
                                }
                                table2.Add(sl.image, sl);
                            }
                            else if (tp.isExplicit)
                            {
                                // This is an error even if the first occurrence was implicit.
                                if (tp.lexStates[i] == ("DEFAULT"))
                                {
                                    JavaCCErrors.SemanticError(sl, "Duplicate definition of string token \"" + sl.image + "\".");
                                }
                                else
                                {
                                    JavaCCErrors.SemanticError(sl, "Duplicate definition of string token \"" + sl.image +
                                            "\" in lexical state \"" + tp.lexStates[i] + "\".");
                                }
                            }
                            else if (re.tpContext.kind != TokenProduction.TOKEN)
                            {
                                JavaCCErrors.SemanticError(sl, "String token \"" + sl.image + "\" has been defined as a \"" +
                                        TokenProduction.kindImage[re.tpContext.kind] + "\" token.");
                            }
                            else if (re.private_rexp)
                            {
                                JavaCCErrors.SemanticError(sl, "String token \"" + sl.image +
                                        "\" has been defined as a private regular expression.");
                            }
                            else
                            {
                                // This is now a legitimate reference to an existing RStringLiteral.
                                // So we assign it a number and take it out of "rexprlist".
                                // Therefore, if all is OK (no errors), then there will be only unequal
                                // string literals in each lexical state.  Note that the only way
                                // this can be legal is if this is a string declared inline within the
                                // BNF.  Hence, it belongs to only one lexical state - namely "DEFAULT".
                                sl.ordinal = re.ordinal;
                                PrepareToRemove(respecs, res);
                            }
                        }
                    }
                }
                else if (res.rexp is not RJustName)
                {
                    res.rexp.ordinal = tokenCount++;
                }
                if (res.rexp is not RJustName && res.rexp.label != (""))
                {
                    names_of_tokens.Add((res.rexp.ordinal), res.rexp.label);
                }
                if (!(res.rexp is RJustName))
                {
                    rexps_of_tokens.Add((res.rexp.ordinal), res.rexp);
                }
            }
        }

        RemovePreparedItems();

        /*
         * The following code performs a tree walk on all regular expressions
         * attaching links to "RJustName"s.  Error messages are given if
         * undeclared names are used, or if "RJustNames" refer to private
         * regular expressions or to regular expressions of any kind other
         * than TOKEN.  In addition, this loop also removes top level
         * "RJustName"s from "rexprlist".
         * This code is not executed if Options.getUserTokenManager() is set to
         * true.  Instead the following block of code is executed.
         */

        if (!Options.GetUserTokenManager())
        {
            var frjn = new FixRJustNames();
            foreach(var tp in rexprlist)
            {
                List<RegExprSpec> respecs = tp.respecs;
                foreach(var res in respecs)
                {
                    frjn.root = res.rexp;
                    ExpansionTreeWalker.PreOrderWalk(res.rexp, frjn);
                    if (res.rexp is RJustName)
                    {
                        PrepareToRemove(respecs, res);
                    }
                }
            }
        }

        RemovePreparedItems();

        /*
         * The following code is executed only if Options.getUserTokenManager() is
         * set to true.  This code visits all top-level "RJustName"s (ignores
         * "RJustName"s nested within regular expressions).  Since regular expressions
         * are optional in this case, "RJustName"s without corresponding regular
         * expressions are given ordinal values here.  If "RJustName"s refer to
         * a named regular expression, their ordinal values are set to reflect this.
         * All but one "RJustName" node is removed from the lists by the end of
         * execution of this code.
         */

        if (Options.GetUserTokenManager())
        {
            foreach(var tp in rexprlist)
            {
                List<RegExprSpec> respecs = tp.respecs;
                foreach(var res in respecs)
                {
                    if (res.rexp is RJustName)
                    {
                        RJustName jn = (RJustName)res.rexp;
                        if (named_tokens_table.TryGetValue(jn.label,out var rexp))
                        {
                            jn.ordinal = tokenCount++;
                            named_tokens_table.Add(jn.label, jn);
                            ordered_named_tokens.Add(jn);
                            names_of_tokens.Add((jn.ordinal), jn.label);
                        }
                        else
                        {
                            jn.ordinal = rexp.ordinal;
                            PrepareToRemove(respecs, res);
                        }
                    }
                }
            }
        }

        RemovePreparedItems();

        /*
         * The following code is executed only if Options.getUserTokenManager() is
         * set to true.  This loop labels any unlabeled regular expression and
         * prints a warning that it is doing so.  These labels are added to
         * "ordered_named_tokens" so that they may be generated into the ...Constants
         * file.
         */
        if (Options.GetUserTokenManager())
        {
            foreach(var tp in rexprlist)
            {
                List<RegExprSpec> respecs = tp.respecs;
                foreach(var res in respecs)
                {
                    int ii = (res.rexp.ordinal);
                    if (!names_of_tokens.TryGetValue(ii,out var _))
                    {
                        JavaCCErrors.Warning(res.rexp, "Unlabeled regular expression cannot be referred to by " +
                                "user generated token manager.");
                    }
                }
            }
        }

        if (JavaCCErrors.GetErrorCount() != 0) throw new MetaParseException();

        // The following code sets the value of the "emptyPossible" field of NormalProduction
        // nodes.  This field is initialized to false, and then the entire list of
        // productions is processed.  This is repeated as long as at least one item
        // got updated from false to true in the pass.
        bool emptyUpdate = true;
        while (emptyUpdate)
        {
            emptyUpdate = false;
            foreach(var prod in bnfproductions)
            {
                if (EmptyExpansionExists(prod.GetExpansion()))
                {
                    if (!prod.IsEmptyPossible())
                    {
                        emptyUpdate = prod.SetEmptyPossible(true);
                    }
                }
            }
        }

        if (Options.GetSanityCheck() && JavaCCErrors.GetErrorCount() == 0)
        {

            // The following code checks that all ZeroOrMore, ZeroOrOne, and OneOrMore nodes
            // do not contain expansions that can expand to the empty token list.
            foreach (var prod in bnfproductions)
            {
                ExpansionTreeWalker.PreOrderWalk(prod.GetExpansion(), new EmptyChecker());
            }

            // The following code goes through the productions and adds pointers to other
            // productions that it can expand to without consuming any tokens.  Once this is
            // done, a left-recursion check can be performed.
            foreach(var prod in bnfproductions)
            {
                AddLeftMost(prod, prod.GetExpansion());
            }

            // Now the following loop calls a recursive walk routine that searches for
            // actual left recursions.  The way the algorithm is coded, once a node has
            // been determined to participate in a left recursive loop, it is not tried
            // in any other loop.
            foreach(var prod in bnfproductions)
            {
                if (prod.GetWalkStatus() == 0)
                {
                    ProdWalk(prod);
                }
            }

            // Now we do a similar, but much simpler walk for the regular expression part of
            // the grammar.  Here we are looking for any kind of loop, not just left recursions,
            // so we only need to do the equivalent of the above walk.
            // This is not done if option USER_TOKEN_MANAGER is set to true.
            if (!Options.GetUserTokenManager())
            {
                foreach(var tp in rexprlist)
                {
                    List<RegExprSpec> respecs = tp.respecs;
                    foreach( var res in respecs)    
                    {
                        RegularExpression rexp = res.rexp;
                        if (rexp.walkStatus == 0)
                        {
                            rexp.walkStatus = -1;
                            if (RexpWalk(rexp))
                            {
                                loopString = "..." + rexp.label + "... --> " + loopString;
                                JavaCCErrors.SemanticError(rexp, "Loop in regular expression detected: \"" + loopString + "\"");
                            }
                            rexp.walkStatus = 1;
                        }
                    }
                }
            }

            /*
             * The following code performs the lookahead ambiguity checking.
             */
            if (JavaCCErrors.GetErrorCount() == 0)
            {
                foreach(var tp in bnfproductions)
                {
                    ExpansionTreeWalker.PreOrderWalk(tp.GetExpansion(), new LookaheadChecker());
                }
            }

        } // matches "if (Options.getSanityCheck()) {"

        if (JavaCCErrors.GetErrorCount() != 0) throw new MetaParseException();

    }

    public static RegularExpression other;

    // Checks to see if the "str" is superseded by another equal (except case) string
    // in table.
    public static bool HasIgnoreCase(Dictionary<string, RegularExpression> table, string str)
    {
        RegularExpression rexp = table[str];
        if (rexp != null && !rexp.tpContext.ignoreCase)
        {
            return false;
        }
        foreach(var ret in table.Values)
        {
            rexp = ret;
            if (rexp.tpContext.ignoreCase)
            {
                other = rexp;
                return true;
            }
        }
        return false;
    }

    // returns true if "exp" can expand to the empty string, returns false otherwise.
    public static bool EmptyExpansionExists(Expansion exp)
    {
        if (exp is NonTerminal terminal)
        {
            return terminal.GetProd().IsEmptyPossible();
        }
        else if (exp is Action)
        {
            return true;
        }
        else if (exp is RegularExpression)
        {
            return false;
        }
        else if (exp is OneOrMore more)
        {
            return EmptyExpansionExists(more.expansion);
        }
        else if (exp is ZeroOrMore || exp is ZeroOrOne)
        {
            return true;
        }
        else if (exp is Lookahead)
        {
            return true;
        }
        else if (exp is Choice choice)
        {
            foreach(var e in choice.GetChoices())
            {
                if (EmptyExpansionExists(e))
                {
                    return true;
                }
            }
            return false;
        }
        else if (exp is Sequence sequence)
        {
            foreach(var e in sequence.units)
            {
                if (!EmptyExpansionExists(e))
                {
                    return false;
                }
            }
            return true;
        }
        else if (exp is TryBlock)
        {
            return EmptyExpansionExists(((TryBlock)exp).exp);
        }
        else
        {
            return false; // This should be dead code.
        }
    }

    // Updates prod.leftExpansions based on a walk of exp.
    static private void AddLeftMost(NormalProduction prod, Expansion exp)
    {
        if (exp is NonTerminal terminal)
        {
            for (int i = 0; i < prod.leIndex; i++)
            {
                if (prod.GetLeftExpansions()[i] == terminal.GetProd())
                {
                    return;
                }
            }
            if (prod.leIndex == prod.GetLeftExpansions().Length)
            {
                NormalProduction[] newle = new NormalProduction[prod.leIndex * 2];
                Array.Copy(prod.GetLeftExpansions(), 0, newle, 0, prod.leIndex);
                prod.SetLeftExpansions(newle);
            }
            prod.GetLeftExpansions()[prod.leIndex++] = terminal.GetProd();
        }
        else if (exp is OneOrMore more1)
        {
            AddLeftMost(prod, more1.expansion);
        }
        else if (exp is ZeroOrMore more)
        {
            AddLeftMost(prod, more.expansion);
        }
        else if (exp is ZeroOrOne one)
        {
            AddLeftMost(prod, one.expansion);
        }
        else if (exp is Choice choice)
        {
            foreach(var p in choice.GetChoices())
            {
                AddLeftMost(prod, p);
            }
        }
        else if (exp is Sequence seq)
        {
            foreach(var e in seq.units)
            {
                AddLeftMost(prod, e);
                if (!EmptyExpansionExists(e))
                {
                    break;
                }
            }
        }
        else if (exp is TryBlock block)
        {
            AddLeftMost(prod, block.exp);
        }
    }

    // The string in which the following methods store information.
    static private string loopString;

    // Returns true to indicate an unraveling of a detected left recursion loop,
    // and returns false otherwise.
    static private bool ProdWalk(NormalProduction prod)
    {
        prod.SetWalkStatus(-1);
        for (int i = 0; i < prod.leIndex; i++)
        {
            if (prod.GetLeftExpansions()[i].GetWalkStatus() == -1)
            {
                prod.GetLeftExpansions()[i].SetWalkStatus(-2);
                loopString = prod.GetLhs() + "... --> " + prod.GetLeftExpansions()[i].GetLhs() + "...";
                if (prod.GetWalkStatus() == -2)
                {
                    prod.SetWalkStatus(1);
                    JavaCCErrors.SemanticError(prod, "Left recursion detected: \"" + loopString + "\"");
                    return false;
                }
                else
                {
                    prod.SetWalkStatus(1);
                    return true;
                }
            }
            else if (prod.GetLeftExpansions()[i].GetWalkStatus() == 0)
            {
                if (ProdWalk(prod.GetLeftExpansions()[i]))
                {
                    loopString = prod.GetLhs() + "... --> " + loopString;
                    if (prod.GetWalkStatus() == -2)
                    {
                        prod.SetWalkStatus(1);
                        JavaCCErrors.SemanticError(prod, "Left recursion detected: \"" + loopString + "\"");
                        return false;
                    }
                    else
                    {
                        prod.SetWalkStatus(1);
                        return true;
                    }
                }
            }
        }
        prod.SetWalkStatus(1);
        return false;
    }

    // Returns true to indicate an unraveling of a detected loop,
    // and returns false otherwise.
    static private bool RexpWalk(RegularExpression rexp)
    {
        if (rexp is RJustName jn)
        {
            if (jn.regexpr.walkStatus == -1)
            {
                jn.regexpr.walkStatus = -2;
                loopString = "..." + jn.regexpr.label + "...";
                // Note: Only the regexpr's of RJustName nodes and the top leve
                // regexpr's can have labels.  Hence it is only in these cases that
                // the labels are checked for to be added to the loopString.
                return true;
            }
            else if (jn.regexpr.walkStatus == 0)
            {
                jn.regexpr.walkStatus = -1;
                if (RexpWalk(jn.regexpr))
                {
                    loopString = "..." + jn.regexpr.label + "... --> " + loopString;
                    if (jn.regexpr.walkStatus == -2)
                    {
                        jn.regexpr.walkStatus = 1;
                        JavaCCErrors.SemanticError(jn.regexpr, "Loop in regular expression detected: \"" + loopString + "\"");
                        return false;
                    }
                    else
                    {
                        jn.regexpr.walkStatus = 1;
                        return true;
                    }
                }
                else
                {
                    jn.regexpr.walkStatus = 1;
                    return false;
                }
            }
        }
        else if (rexp is RChoice ch)
        {
            foreach(var rex in ch.GetChoices())
            {
                if (rex is RegularExpression re && RexpWalk(re))
                {
                    return true;
                }
            }
            return false;
        }
        else if (rexp is RSequence rseq)
        {
            foreach(var u in rseq.units)
            {
                if (RexpWalk(u))
                {
                    return true;
                }
            }
            return false;
        }
        else if (rexp is ROneOrMore more1)
        {
            return RexpWalk(more1.regexpr);
        }
        else if (rexp is RZeroOrMore more)
        {
            return RexpWalk(more.regexpr);
        }
        else if (rexp is RZeroOrOne one)
        {
            return RexpWalk(one.regexpr);
        }
        else if (rexp is RRepetitionRange range)
        {
            return RexpWalk(range.regexpr);
        }
        return false;
    }

    /**
     * Objects of this class are created from class Semanticize to work on
     * references to regular expressions from RJustName's.
     */
    class FixRJustNames : JavaCCGlobals, TreeWalkerOp
    {

        public RegularExpression root;

        public bool GoDeeper(Expansion e)
        {
            return true;
        }

        public void Action(Expansion e)
        {
            if (e is RJustName jn)
            {
                if (!named_tokens_table.TryGetValue(jn.label,out var rexp))
                {
                    JavaCCErrors.SemanticError(e, "Undefined lexical token name \"" + jn.label + "\".");
                }
                else if (jn == root && !jn.tpContext.isExplicit && rexp.private_rexp)
                {
                    JavaCCErrors.SemanticError(e, "Token name \"" + jn.label + "\" refers to a private " +
                            "(with a #) regular expression.");
                }
                else if (jn == root && !jn.tpContext.isExplicit && rexp.tpContext.kind != TokenProduction.TOKEN)
                {
                    JavaCCErrors.SemanticError(e, "Token name \"" + jn.label + "\" refers to a non-token " +
                            "(SKIP, MORE, IGNORE_IN_BNF) regular expression.");
                }
                else
                {
                    jn.ordinal = rexp.ordinal;
                    jn.regexpr = rexp;
                }
            }
        }

    }

    class LookaheadFixer : JavaCCGlobals, TreeWalkerOp
    {

        public bool GoDeeper(Expansion e)
        {
            if (e is RegularExpression)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public void Action(Expansion e)
        {
            if (e is Sequence sequence)
            {
                if (e.parent is Choice || e.parent is ZeroOrMore ||
                    e.parent is OneOrMore || e.parent is ZeroOrOne)
                {
                    return;
                }
                Sequence seq = sequence;
                Lookahead la = (Lookahead)(seq.units[0]);
                if (!la.IsExplicit())
                {
                    return;
                }
                // Create a singleton choice with an empty action.
                Choice ch = new Choice();
                ch.SetLine(la.GetLine()); ch.SetColumn(la.GetColumn());
                ch.parent = seq;
                Sequence seq1 = new Sequence();
                seq1.SetLine(la.GetLine()); seq1.SetColumn(la.GetColumn());
                seq1.parent = ch;
                seq1.units.Add(la);
                la.parent = seq1;
                Action act = new Action();
                act.SetLine(la.GetLine()); act.SetColumn(la.GetColumn());
                act.parent = seq1;
                seq1.units.Add(act);
                ch.GetChoices().Add(seq1);
                if (la.GetAmount() != 0)
                {
                    if (la.GetActionTokens().Count != 0)
                    {
                        JavaCCErrors.Warning(la, "Encountered LOOKAHEAD(...) at a non-choice location.  " +
                                "Only semantic lookahead will be considered here.");
                    }
                    else
                    {
                        JavaCCErrors.Warning(la, "Encountered LOOKAHEAD(...) at a non-choice location.  This will be ignored.");
                    }
                }
                // Now we have moved the lookahead into the singleton choice.  Now create
                // a new dummy lookahead node to replace this one at its original location.
                Lookahead la1 = new Lookahead();
                la1.SetExplicit(false);
                la1.SetLine(la.GetLine()); la1.SetColumn(la.GetColumn());
                la1.parent = seq;
                // Now set the la_expansion field of la and la1 with a dummy expansion (we use EOF).
                la.SetLaExpansion(new REndOfFile());
                la1.SetLaExpansion(new REndOfFile());
                seq.units[0] = la1;
                seq.units.Insert(1, ch);
            }
        }

    }

    class ProductionDefinedChecker : JavaCCGlobals, TreeWalkerOp
    {

        public bool GoDeeper(Expansion e)
        {
            if (e is RegularExpression)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public void Action(Expansion e)
        {
            if (e is NonTerminal nt)
            {                
                if (production_table.TryGetValue(nt.GetName(), out var ret))
                {
                    nt.SetProd(ret);

                    JavaCCErrors.SemanticError(e, "Non-terminal " + nt.GetName() + " has not been defined.");
                }
                else
                {
                    nt.GetProd().GetParents().Add(nt);
                }
            }
        }

    }

    class EmptyChecker : JavaCCGlobals , TreeWalkerOp
    {

        public bool GoDeeper(Expansion e)
        {
            if (e is RegularExpression)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public void Action(Expansion e)
        {
            if (e is OneOrMore more1)
            {
                if (Semanticize.EmptyExpansionExists(more1.expansion))
                {
                    JavaCCErrors.SemanticError(e, "Expansion within \"(...)+\" can be matched by empty string.");
                }
            }
            else if (e is ZeroOrMore more)
            {
                if (Semanticize.EmptyExpansionExists(more.expansion))
                {
                    JavaCCErrors.SemanticError(e, "Expansion within \"(...)*\" can be matched by empty string.");
                }
            }
            else if (e is ZeroOrOne one)
            {
                if (Semanticize.EmptyExpansionExists(one.expansion))
                {
                    JavaCCErrors.SemanticError(e, "Expansion within \"(...)?\" can be matched by empty string.");
                }
            }
        }

    }

    class LookaheadChecker : JavaCCGlobals, TreeWalkerOp
    {

        public bool GoDeeper(Expansion e)
        {
            if (e is RegularExpression)
            {
                return false;
            }
            else if (e is Lookahead)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public void Action(Expansion e)
        {
            if (e is Choice choice)
            {
                if (Options.GetLookahead() == 1 || Options.GetForceLaCheck())
                {
                    LookaheadCalc.ChoiceCalc(choice);
                }
            }
            else if (e is OneOrMore exp)
            {
                if (Options.GetForceLaCheck() || (ImplicitLA(exp.expansion) && Options.GetLookahead() == 1))
                {
                    LookaheadCalc.EbnfCalc(exp, exp.expansion);
                }
            }
            else if (e is ZeroOrMore exp2)
            {
                if (Options.GetForceLaCheck() || (ImplicitLA(exp2.expansion) && Options.GetLookahead() == 1))
                {
                    LookaheadCalc.EbnfCalc(exp2, exp2.expansion);
                }
            }
            else if (e is ZeroOrOne exp3)
            {   if (Options.GetForceLaCheck() || (ImplicitLA(exp3.expansion) && Options.GetLookahead() == 1))
                {
                    LookaheadCalc.EbnfCalc(exp3, exp3.expansion);
                }
            }
        }

        static bool ImplicitLA(Expansion exp)
            => exp is not Sequence seq || seq.units[0] is not Lookahead la || !la.IsExplicit();

    }

    public static new void ReInit()
    {
        removeList = new();
        itemList = new();
        other = null;
        loopString = null;
    }

}
