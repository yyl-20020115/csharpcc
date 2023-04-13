using System.Text;

namespace CSharpCC.CCTree;

/**
 * An implementation of interface CharStream, where the stream is assumed to
 * contain only ASCII characters (with java-like unicode escape processing).
 */

public class CSharpCharStream
{
    public static bool StaticFlag = false;

    public static int Hexval(char c) => c switch
    {
        '0' => 0,
        '1' => 1,
        '2' => 2,
        '3' => 3,
        '4' => 4,
        '5' => 5,
        '6' => 6,
        '7' => 7,
        '8' => 8,
        '9' => 9,
        'a' or 'A' => 10,
        'b' or 'B' => 11,
        'c' or 'C' => 12,
        'd' or 'D' => 13,
        'e' or 'E' => 14,
        'f' or 'F' => 15,
        _ => throw new IOException(),// Should never come here
    };

    public int bufpos = -1;
    int bufsize;
    int available;
    int tokenBegin;
    protected int[] bufline;
    protected int[] bufcolumn;

    protected int column = 0;
    protected int line = 1;

    protected bool prevCharIsCR = false;
    protected bool prevCharIsLF = false;

    protected TextReader inputStream;

    protected char[] nextCharBuf;
    protected char[] buffer;
    protected int maxNextCharInd = 0;
    protected int nextCharInd = -1;
    protected int inBuf = 0;
    protected int tabSize = 8;

    protected int TabSize { get => tabSize;
        set => tabSize = value; }

    protected void ExpandBuff(bool wrapAround)
    {
        var newbuffer = new char[bufsize + 2048];
        var newbufline = new int[bufsize + 2048];
        var newbufcolumn = new int[bufsize + 2048];

        try
        {
            if (wrapAround)
            {
                Array.Copy(buffer, tokenBegin, newbuffer, 0, bufsize - tokenBegin);
                Array.Copy(buffer, 0, newbuffer, bufsize - tokenBegin, bufpos);
                buffer = newbuffer;

                Array.Copy(bufline, tokenBegin, newbufline, 0, bufsize - tokenBegin);
                Array.Copy(bufline, 0, newbufline, bufsize - tokenBegin, bufpos);
                bufline = newbufline;

                Array.Copy(bufcolumn, tokenBegin, newbufcolumn, 0, bufsize - tokenBegin);
                Array.Copy(bufcolumn, 0, newbufcolumn, bufsize - tokenBegin, bufpos);
                bufcolumn = newbufcolumn;

                bufpos += (bufsize - tokenBegin);
            }
            else
            {
                Array.Copy(buffer, tokenBegin, newbuffer, 0, bufsize - tokenBegin);
                buffer = newbuffer;

                Array.Copy(bufline, tokenBegin, newbufline, 0, bufsize - tokenBegin);
                bufline = newbufline;

                Array.Copy(bufcolumn, tokenBegin, newbufcolumn, 0, bufsize - tokenBegin);
                bufcolumn = newbufcolumn;

                bufpos -= tokenBegin;
            }
        }
        catch (Exception t)
        {
            throw new Exception(t.Message);
        }

        available = (bufsize += 2048);
        tokenBegin = 0;
    }

    protected void FillBuff()
    {
        int i;
        if (maxNextCharInd == 4096)
            maxNextCharInd = nextCharInd = 0;

        try
        {
            if ((i = inputStream.Read(nextCharBuf, maxNextCharInd,
                                                4096 - maxNextCharInd)) == -1)
            {
                inputStream.Close();
                throw new IOException();
            }
            else
                maxNextCharInd += i;
            return;
        }
        catch (IOException e)
        {
            if (bufpos != 0)
            {
                --bufpos;
                Backup(0);
            }
            else
            {
                bufline[bufpos] = line;
                bufcolumn[bufpos] = column;
            }
            throw e;
        }
    }

    protected char ReadByte()
    {
        if (++nextCharInd >= maxNextCharInd)
            FillBuff();

        return nextCharBuf[nextCharInd];
    }

    public char BeginToken()
    {
        if (inBuf > 0)
        {
            --inBuf;

            if (++bufpos == bufsize)
                bufpos = 0;

            tokenBegin = bufpos;
            return buffer[bufpos];
        }

        tokenBegin = 0;
        bufpos = -1;

        return ReadChar();
    }

    protected void AdjustBuffSize()
    {
        if (available == bufsize)
        {
            if (tokenBegin > 2048)
            {
                bufpos = 0;
                available = tokenBegin;
            }
            else
                ExpandBuff(false);
        }
        else if (available > tokenBegin)
            available = bufsize;
        else if ((tokenBegin - available) < 2048)
            ExpandBuff(true);
        else
            available = tokenBegin;
    }

    protected void UpdateLineColumn(char c)
    {
        column++;

        if (prevCharIsLF)
        {
            prevCharIsLF = false;
            line += (column = 1);
        }
        else if (prevCharIsCR)
        {
            prevCharIsCR = false;
            if (c == '\n')
            {
                prevCharIsLF = true;
            }
            else
                line += (column = 1);
        }

        switch (c)
        {
            case '\r':
                prevCharIsCR = true;
                break;
            case '\n':
                prevCharIsLF = true;
                break;
            case '\t':
                column--;
                column += (tabSize - (column % tabSize));
                break;
            default:
                break;
        }

        bufline[bufpos] = line;
        bufcolumn[bufpos] = column;
    }

    public char ReadChar()
    {
        if (inBuf > 0)
        {
            --inBuf;

            if (++bufpos == bufsize)
                bufpos = 0;

            return buffer[bufpos];
        }

        char c;

        if (++bufpos == available)
            AdjustBuffSize();

        if ((buffer[bufpos] = c = ReadByte()) == '\\')
        {
            UpdateLineColumn(c);

            int backSlashCnt = 1;

            for (; ; ) // Read all the backslashes
            {
                if (++bufpos == available)
                    AdjustBuffSize();
                try
                {
                    if ((buffer[bufpos] = c = ReadByte()) != '\\')
                    {
                        UpdateLineColumn(c);
                        // found a non-backslash char.
                        if ((c == 'u') && ((backSlashCnt & 1) == 1))
                        {
                            if (--bufpos < 0)
                                bufpos = bufsize - 1;

                            break;
                        }

                        Backup(backSlashCnt);
                        return '\\';
                    }
                }
                catch (IOException)
                {
                    // We are returning one backslash so we should only backup (count-1)
                    if (backSlashCnt > 1)
                        Backup(backSlashCnt - 1);

                    return '\\';
                }

                UpdateLineColumn(c);
                backSlashCnt++;
            }

            // Here, we have seen an odd number of backslash's followed by a 'u'
            try
            {
                while ((c = ReadByte()) == 'u')
                    ++column;

                buffer[bufpos] = c = (char)(Hexval(c) << 12 |
                                            Hexval(ReadByte()) << 8 |
                                            Hexval(ReadByte()) << 4 |
                                            Hexval(ReadByte()));

                column += 4;
            }
            catch (IOException)
            {
                throw new Error("Invalid escape character at line " + line +
                                                 " column " + column + ".");
            }

            if (backSlashCnt == 1)
                return c;
            else
            {
                Backup(backSlashCnt - 1);
                return '\\';
            }
        }
        else
        {
            UpdateLineColumn(c);
            return c;
        }
    }

    //@Deprecated
    /**
     * @deprecated
     * @see #getEndColumn
     */
    public int Column => bufcolumn[bufpos];

    //@Deprecated
    /**
     * @deprecated
     * @see #getEndLine
     */
    public int Line => bufline[bufpos];

    /** Get end column. */
    public int EndColumn => bufcolumn[bufpos];

    /** Get end line. */
    public int EndLine => bufline[bufpos];

    /** @return column of token start */
    public int BeginColumn => bufcolumn[tokenBegin];

    /** @return line number of token start */
    public int BeginLine => bufline[tokenBegin];

    /** Retreat. */
    public void Backup(int amount)
    {

        inBuf += amount;
        if ((bufpos -= amount) < 0)
            bufpos += bufsize;
    }

    /** Constructor. */
    public CSharpCharStream(TextReader dstream,int startline, int startcolumn, int buffersize)
    {
        inputStream = dstream;
        line = startline;
        column = startcolumn - 1;

        available = bufsize = buffersize;
        buffer = new char[buffersize];
        bufline = new int[buffersize];
        bufcolumn = new int[buffersize];
        nextCharBuf = new char[4096];
    }

    /** Constructor. */
    public CSharpCharStream(TextReader dstream, int startline, int startcolumn)
        : this(dstream, startline, startcolumn, 4096) { }

    /** Constructor. */
    public CSharpCharStream(TextReader dstream)
        : this(dstream, 1, 1, 4096) { }
    /** Reinitialise. */
    public void ReInit(TextReader dstream,int startline, int startcolumn, int buffersize)
    {
        inputStream = dstream;
        line = startline;
        column = startcolumn - 1;

        if (buffer == null || buffersize != buffer.Length)
        {
            available = bufsize = buffersize;
            buffer = new char[buffersize];
            bufline = new int[buffersize];
            bufcolumn = new int[buffersize];
            nextCharBuf = new char[4096];
        }
        prevCharIsLF = prevCharIsCR = false;
        tokenBegin = inBuf = maxNextCharInd = 0;
        nextCharInd = bufpos = -1;
    }

    /** Reinitialise. */
    public void ReInit(TextReader dstream,int startline, int startcolumn)
    {
        ReInit(dstream, startline, startcolumn, 4096);
    }

    /** Reinitialise. */
    public void ReInit(TextReader dstream)
    {
        ReInit(dstream, 1, 1, 4096);
    }
    /** Constructor. */
    public CSharpCharStream(Stream dstream, Encoding encoding, int startline,int startcolumn, int buffersize)
        : this(encoding == null ? new StreamReader(dstream) : new StreamReader(dstream, encoding), startline, startcolumn, buffersize) { }

    public CSharpCharStream(Stream dstream, int startline,int startcolumn, int buffersize)
        : this(new StreamReader(dstream), startline, startcolumn, 4096) { }

    public CSharpCharStream(Stream dstream, Encoding encoding, int startline,int startcolumn)
        : this(dstream, encoding, startline, startcolumn, 4096) { }

    public CSharpCharStream(Stream dstream, int startline,int startcolumn)
        : this(dstream, startline, startcolumn, 4096) { }

    public CSharpCharStream(Stream dstream, Encoding encoding)
        : this(dstream, encoding, 1, 1, 4096) { }

    public CSharpCharStream(Stream dstream)
        : this(dstream, 1, 1, 4096) { }

    public void ReInit(Stream dstream, Encoding encoding, int startline, int startcolumn, int buffersize) => ReInit(encoding == null ? new StreamReader(dstream) : new StreamReader(dstream, encoding), startline, startcolumn, buffersize);
    public void ReInit(Stream dstream, int startline, int startcolumn, int buffersize) => ReInit(new StreamReader(dstream), startline, startcolumn, buffersize);
    public void ReInit(Stream dstream, Encoding encoding, int startline, int startcolumn) => ReInit(dstream, encoding, startline, startcolumn, 4096);
    public void ReInit(Stream dstream, int startline, int startcolumn) => ReInit(dstream, startline, startcolumn, 4096);
    public void ReInit(Stream dstream, Encoding encoding) => ReInit(dstream, encoding, 1, 1, 4096);
    public void ReInit(Stream dstream) => ReInit(dstream, 1, 1, 4096);
    public string GetImage()
    {
        if (bufpos >= tokenBegin)
            return new string(buffer, tokenBegin, bufpos - tokenBegin + 1);
        else
            return new string(buffer, tokenBegin, bufsize - tokenBegin) +
                                    new string(buffer, 0, bufpos + 1);
    }

    public char[] GetSuffix(int len)
    {
        char[] ret = new char[len];

        if ((bufpos + 1) >= len)
            Array.Copy(buffer, bufpos - len + 1, ret, 0, len);
        else
        {
            Array.Copy(buffer, bufsize - (len - bufpos - 1), ret, 0,
                                                              len - bufpos - 1);
            Array.Copy(buffer, 0, ret, len - bufpos - 1, bufpos + 1);
        }

        return ret;
    }
    public void Done()
    {
        nextCharBuf = null;
        buffer = null;
        bufline = null;
        bufcolumn = null;
    }

    /**
     * Method to adjust line and column numbers for the start of a token.
     */
    public void AdjustBeginLineColumn(int newLine, int newCol)
    {
        int start = tokenBegin;
        int len;

        if (bufpos >= tokenBegin)
        {
            len = bufpos - tokenBegin + inBuf + 1;
        }
        else
        {
            len = bufsize - tokenBegin + bufpos + 1 + inBuf;
        }

        int i = 0, j = 0, k = 0;
        int nextColDiff = 0, columnDiff = 0;

        while (i < len && bufline[j = start % bufsize] == bufline[k = ++start % bufsize])
        {
            bufline[j] = newLine;
            nextColDiff = columnDiff + bufcolumn[k] - bufcolumn[j];
            bufcolumn[j] = newCol + columnDiff;
            columnDiff = nextColDiff;
            i++;
        }

        if (i < len)
        {
            bufline[j] = newLine++;
            bufcolumn[j] = newCol + columnDiff;

            while (i++ < len)
            {
                if (bufline[j = start % bufsize] != bufline[++start % bufsize])
                    bufline[j] = newLine++;
                else
                    bufline[j] = newLine;
            }
        }

        line = bufline[j];
        column = bufcolumn[j];
    }

}
