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
namespace org.javacc.jjtree;


public class ASTOptionBinding : JJTreeNode
{
    public ASTOptionBinding(int id) : base(id)
    {
    }

    private bool suppressed = false;
    private string name;

    public void Initialize(string n, string v)
    {
        name = n;

        // If an option is specific to JJTree it should not be written _out
        // to the output file for JavaCC.

        if (JJTreeGlobals.IsOptionJJTreeOnly(name))
        {
            suppressed = true;
        }
    }


    public bool IsSuppressed => suppressed;

    public void SuppressOption(bool s)
    {
        suppressed = s;
    }


    public override string TranslateImage(Token t)
    {
        if (suppressed)
        {
            return WhiteOut(t);
        }
        else
        {
            return t.image;
        }
    }

    /** Accept the visitor. **/
    public override object jjtAccept(JJTreeParserVisitor visitor, object data) 
        s=> visitor.visit(this, data);
}


/*end*/
