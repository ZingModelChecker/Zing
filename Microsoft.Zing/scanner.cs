using System;
using System.Collections;
using System.Collections.Specialized;
using System.Compiler;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Microsoft.Zing
{

    internal sealed class Scanner : System.Compiler.Scanner
    {
        internal bool disableNameMangling = false;
        internal char charLiteralValue;
        private Document document;
        private Document originalDocument;
        private DocumentText sourceText;
        private string sourceString;
        private int startPos;
        internal int endPos; //one more than the last column that contains a character making up the token
        private int maxPos; //one more than the last column that contains a source character
        internal bool TokenIsFirstOnLine;
        internal int eolPos;
        private int lastReportedErrorPos;

        private ErrorNodeList errors;
        internal Hashtable PreprocessorDefinedSymbols;

        private String unescapedString;
        private StringBuilder identifier = new StringBuilder(128);
        private int idLastPosOnBuilder;
        private bool stillInsideMultiLineToken;

        private static readonly Keyword[] Keywords = Keyword.InitKeywords();

        //These fields help the scanner keep track of the preprocesor state
        private bool allowPPDefinitions = true;
        private int includeCount; //increment when included part of #if-#elif-#else-#endif is ecountered
        private int endIfCount;   //increment on #if, decrement on #endif
        private int elseCount;    //increment on #else, assign endIfCount on #endif
        private int endRegionCount;

        internal Scanner()
        {
        }  
        internal Scanner(Document document, ErrorNodeList errors, CompilerOptions options)
        {
            this.document = document;
            this.originalDocument = document;
            this.sourceText = document.Text;
            this.endPos = 0;
            this.maxPos = document.Text.Length;
            this.errors = errors;
            this.lastReportedErrorPos = 0;
            this.PreprocessorDefinedSymbols = new Hashtable();
            this.PreprocessorDefinedSymbols["true"] = "true";
            this.PreprocessorDefinedSymbols["false"] = null;
            if (options != null)
            {
                StringList syms = options.DefinedPreProcessorSymbols;
                for (int i = 0, n = syms == null ? 0 : syms.Count; i < n; i++)
                {
                    string sym = syms[i];
                    if (sym == null) continue;
                    this.PreprocessorDefinedSymbols[sym] = sym;
                }
            }
        }

        public override void SetSource(string source, int offset)
        {
            this.sourceString = source;
            this.endPos = this.startPos = offset;
            this.maxPos = source.Length;
        }
        private string Substring(int start, int length)
        {
            if (this.sourceString != null)
                return this.sourceString.Substring(start, length);
            else
                return this.sourceText.Substring(start, length);
        }
        public override bool ScanTokenAndProvideInfoAboutIt(TokenInfo tokenInfo, ref int state)
        {
            tokenInfo.trigger = TokenTrigger.None;
            Token tok;
            if (state == 1)
            {
                //Already inside a multi-line comment
                if (this.endPos >= this.maxPos) return false;
                this.SkipMultiLineComment(true);
                if (this.stillInsideMultiLineToken)
                    this.stillInsideMultiLineToken = false;
                else
                    state = 0;
                tok = Token.MultiLineComment;
            }
            else
                tok = this.GetNextToken(false);
            switch(tok)
            {
                case Token.Colon:
                case Token.DotDot:
                case Token.Plus:
                case Token.BitwiseAnd:
                case Token.LogicalAnd:
                case Token.Assign:
                case Token.BitwiseOr:
                case Token.LogicalOr:
                case Token.BitwiseXor:
                case Token.LogicalNot:
                case Token.BitwiseNot:
                case Token.Divide:
                case Token.Equal:
                case Token.GreaterThan:
                case Token.GreaterThanOrEqual:
                case Token.LeftShift:
                case Token.LessThan:
                case Token.LessThanOrEqual:
                case Token.Remainder:
                case Token.Multiply:
                case Token.NotEqual:
                case Token.Arrow:
                case Token.RightShift:
                case Token.Subtract:
                    tokenInfo.color = TokenColor.Text;
                    tokenInfo.type = TokenType.Operator;
                    break;
                case Token.Semicolon:
                    tokenInfo.color = TokenColor.Text;
                    tokenInfo.type = TokenType.Delimiter;
                    break;
                case Token.Activate:
                case Token.Array:
                case Token.Bool:
                case Token.Byte:
                case Token.Chan:
                case Token.Decimal:
                case Token.Double:
                case Token.False:
                case Token.First:
                case Token.Float:
                case Token.Goto:
                case Token.In:
                case Token.Int:
                case Token.Long:
                case Token.New:
                case Token.Null:
                case Token.Object:
                case Token.Out:
                case Token.Range:
                case Token.Raise:
                case Token.Return:
                case Token.SByte:
                case Token.Set:
                case Token.Short:
                case Token.Static:
                case Token.String:
                case Token.This:
                case Token.Timeout:
                case Token.UInt:
                case Token.ULong:
                case Token.UShort:
                case Token.True:
                case Token.Visible:
                case Token.Void:
                    tokenInfo.color = TokenColor.Keyword;
                    tokenInfo.type = TokenType.Keyword;
                    break;
                case Token.Accept:
                case Token.Assert:
                case Token.Assume:
                case Token.Async:
                case Token.Atomic:
                case Token.Choose:
                case Token.Class:
                case Token.Else:
                case Token.End:
                case Token.Enum:
                case Token.Event:
                case Token.Foreach:
                case Token.If:
                case Token.Interface:
                case Token.Receive:
                case Token.Select:
                case Token.Send:
                case Token.Sizeof:
                case Token.Struct:
                case Token.Try:
                case Token.Typeof:
                case Token.Wait:
                case Token.While:
                case Token.With:
                    tokenInfo.trigger = TokenTrigger.MatchBraces;
                    tokenInfo.color = TokenColor.Keyword;
                    tokenInfo.type = TokenType.Keyword;
                    break;
                case Token.Comma:
                    tokenInfo.trigger = TokenTrigger.ParamNext;
                    tokenInfo.color = TokenColor.Text;
                    tokenInfo.type = TokenType.Delimiter;
                    break;
                case Token.HexLiteral:
                case Token.IntegerLiteral:
                case Token.RealLiteral:
                    tokenInfo.color = TokenColor.Number;
                    tokenInfo.type = TokenType.Literal;
                    break;
                case Token.Identifier:
                    tokenInfo.color = TokenColor.Identifier;
                    tokenInfo.type = TokenType.Identifier;
                    break;
                case Token.LeftBracket:
                case Token.LeftParenthesis:
                case Token.LeftBrace:
                    tokenInfo.trigger = TokenTrigger.ParamStart|TokenTrigger.MatchBraces;
                    tokenInfo.color = TokenColor.Text;
                    tokenInfo.type = TokenType.Delimiter;
                    break;
                case Token.Dot:
                    tokenInfo.trigger = TokenTrigger.MemberSelect;
                    tokenInfo.color = TokenColor.Text;
                    tokenInfo.type = TokenType.Delimiter;
                    break;
                case Token.MultiLineComment:
                    tokenInfo.color = TokenColor.Comment;
                    tokenInfo.type = TokenType.Comment;
                    if (this.stillInsideMultiLineToken)
                    {
                        this.stillInsideMultiLineToken = false;
                        state = 1;
                    }
                    break;
                case Token.RightBracket:
                case Token.RightParenthesis:
                case Token.RightBrace:
                    tokenInfo.trigger = TokenTrigger.ParamEnd|TokenTrigger.MatchBraces;
                    tokenInfo.color = TokenColor.Text;
                    tokenInfo.type = TokenType.Delimiter;
                    break;
                case Token.SingleLineComment:
                    tokenInfo.color = TokenColor.Comment;
                    tokenInfo.type = TokenType.LineComment;
                    break;
                case Token.StringLiteral:
                    tokenInfo.color = TokenColor.String;
                    tokenInfo.type = TokenType.String;
                    break;
                case Token.EndOfFile:
                    return false;
                default:
                    tokenInfo.color = TokenColor.Text;
                    tokenInfo.type = TokenType.Delimiter;
                    break;
            }
            tokenInfo.startIndex = this.startPos;
            tokenInfo.endIndex = this.endPos-1;
            return true;
        }
        internal Token GetNextToken()
        {
            return this.GetNextToken(true);
        }
        private Token GetNextToken(bool suppressComments)
        {
            Token token = Token.None;
            this.TokenIsFirstOnLine = false;
            nextToken:
                this.identifier.Length = 0;
            char c = this.SkipBlanks();
            this.startPos = this.endPos-1;
            switch (c)
            {
                case (char)0:
                    if (this.endPos < this.maxPos) goto nextToken; //silenty skip over explicit null char in source
                    this.startPos = this.endPos;
                    token = Token.EndOfFile; //Null char was signal from SkipBlanks that end of source has been reached
                    this.TokenIsFirstOnLine = true;
                    if (this.endRegionCount > 0)
                        this.HandleError(Error.EndRegionDirectiveExpected);
                    else if (this.endIfCount > 0)
                        this.HandleError(Error.EndifDirectiveExpected);
                    break;
                case '+':
                    token = Token.Plus;
                    break;
                case ':':
                    token = Token.Colon;
                    break;
                case ',':
                    token = Token.Comma;
                    break;
                case '=':
                    token = Token.Assign;
                    c = this.GetChar(this.endPos);
                    if (c == '=')
                    {
                        token = Token.Equal; this.endPos++;
                    }
                    break;
                case '[':
                    token = Token.LeftBracket;
                    break;
                case '(':
                    token = Token.LeftParenthesis;
                    break;
                case '{':
                    token = Token.LeftBrace;
                    break;
                case '.':
                    token = Token.Dot;
                    c = this.GetChar(this.endPos);
                    if (c == '.')
                    {
                        token = Token.DotDot; this.endPos++;
                    }
                    break;
                case '*':
                    token = Token.Multiply;
                    break;
                case '%':
                    token = Token.Remainder;
                    break;
                case '^':
                    token = Token.BitwiseXor;
                    break;
                case '~':
                    token = Token.BitwiseNot;
                    break;
                case '!':
                    token = Token.LogicalNot;
                    c = this.GetChar(this.endPos);
                    if (c == '=')
                    {
                        token = Token.NotEqual; this.endPos++;
                    }
                    break;
                case '&':
                    token = Token.BitwiseAnd;
                    c = this.GetChar(this.endPos);
                    if (c == '&')
                    {
                        token = Token.LogicalAnd; this.endPos++;
                    }
                    break;
                case '|':
                    token = Token.BitwiseOr;
                    c = this.GetChar(this.endPos);
                    if (c == '|')
                    {
                        token = Token.LogicalOr; this.endPos++;
                    }
                    break;
                case ']':
                    token = Token.RightBracket;
                    break;
                case ')':
                    token = Token.RightParenthesis;
                    break;
                case '}':
                    token = Token.RightBrace;
                    break;
                case ';':
                    token = Token.Semicolon;
                    break;
                case '"':
                    token = Token.StringLiteral;
                    this.ScanString('"');
                    break;
                case '-':
                    token = Token.Subtract;
                    c = this.GetChar(this.endPos);
                    if (c == '>')
                    {
                        token = Token.Arrow; this.endPos++;
                    }
                    break;
                case '>':
                    token = Token.GreaterThan;
                    c = this.GetChar(this.endPos);
                    if (c == '=')
                    {
                        token = Token.GreaterThanOrEqual; this.endPos++;
                    }
                    else if (c == '>')
                    {
                        token = Token.RightShift; this.endPos++;
                    }
                    break;
                case '<':
                    token = Token.LessThan;
                    c = this.GetChar(this.endPos);
                    if (c == '=')
                    {
                        token = Token.LessThanOrEqual; this.endPos++;
                    }
                    else if (c == '<')
                    {
                        token = Token.LeftShift; this.endPos++;
                    }
                    break;
                case '/':
                    token = Token.Divide;
                    c = this.GetChar(this.endPos);
                    switch (c)
                    {
                        case '/':
                            this.SkipSingleLineComment();
                            if (suppressComments)
                            {
                                if (this.endPos >= this.maxPos)
                                {
                                    token = Token.EndOfFile;
                                    break; // just break out and return
                                }
                                goto nextToken; // read another token this last one was a comment
                            }
                            else
                            {
                                token = Token.SingleLineComment;
                                break;
                            }
                        case '*':
                            this.endPos++;
                            if (suppressComments)
                            {
                                int savedEndPos = this.endPos;
                                this.SkipMultiLineComment(false);
                                if (this.endPos >= this.maxPos && this.GetChar(this.maxPos-1) != '/')
                                {
                                    this.endPos = savedEndPos;
                                    this.HandleError(Error.NoCommentEnd);
                                    token = Token.EndOfFile;
                                    this.endPos = this.maxPos;
                                    break;
                                }
                                goto nextToken; // read another token this last one was a comment
                            }
                            else
                            {
                                this.SkipMultiLineComment(true);
                                token = Token.MultiLineComment;
                                break;
                            }
                    }
                    break;
                case '\\':
                    this.endPos--;
                    if (this.IsIdentifierStartChar(c))
                    {
                        token = Token.Identifier;
                        this.endPos++;
                        this.ScanIdentifier();
                        break;
                    }
                    this.ScanEscapedChar();
                    token = Token.IllegalCharacter;
                    this.endPos++;
                    break;
                case '@':
                    c = this.GetChar(this.endPos++);
                    if (c == '"')
                    {
                        token = Token.StringLiteral;
                        this.ScanVerbatimString(!suppressComments);
                        if (this.stillInsideMultiLineToken)
                            this.stillInsideMultiLineToken = false;
                        break;
                    }
                    if ('a' <= c && c <= 'z' || 'A' <= c && c <= 'Z' || c == '_' || Scanner.IsUnicodeLetter(c))
                    {
                        token = Token.Identifier;
                        this.ScanIdentifier();
                    }
                    else
                        token = Token.IllegalCharacter;
                    break;
                case '#':
                    int endIfCount = this.endIfCount;
                    int includeCount = this.includeCount;
                    bool exclude = this.ScanPreProcessorDirective(!suppressComments, false, true);
                    if (endIfCount == this.endIfCount) endIfCount--;
                    if (!suppressComments)
                    {
                        token = Token.Plus;
                        this.endPos++;
                        break;
                    }
                    if (!exclude) goto nextToken;
                    while (this.endIfCount > endIfCount)
                    {
                        char ch = this.SkipBlanks();
                        this.startPos = this.endPos-1;
                        if (ch == '#')
                        {
                            this.includeCount = includeCount;
                            exclude = this.ScanPreProcessorDirective(false, true, this.endIfCount <= endIfCount+1);
                            if (!exclude && this.endIfCount == endIfCount+1) break;
                        }
                        else if (ch == 0 && this.endPos >= this.maxPos)
                        {
                            this.endPos = this.maxPos;
                            this.HandleError(Error.EndifDirectiveExpected);
                            return Token.EndOfFile;
                        }
                        else
                            this.SkipSingleLineComment();
                    }
                    goto nextToken;
                    // line terminators
                case '\r':
                    this.TokenIsFirstOnLine = true;
                    this.eolPos = this.endPos;
                    if (this.GetChar(this.endPos) == '\n') this.endPos++;
                    goto nextToken;
                case '\n':
                case (char)0x2028:
                case (char)0x2029:
                    this.eolPos = this.endPos;
                    this.TokenIsFirstOnLine = true;
                    goto nextToken;
                default:
                    if ('a' <= c && c <= 'z')
                        token = this.ScanKeyword(c);
                    else if ('A' <= c && c <= 'Z' || c == '_')
                    {
                        token = Token.Identifier;
                        this.ScanIdentifier();
                    }
                    else if (Scanner.IsDigit(c))
                        token = this.ScanNumber(c);
                    else if (Scanner.IsUnicodeLetter(c))
                    {
                        token = Token.Identifier;
                        this.ScanIdentifier();
                    }
                    else
                        token = Token.IllegalCharacter;
                    break;
            }
            this.allowPPDefinitions = false;
            return token;
        }
        private char GetChar(int index)
        {
            if (index < this.maxPos)
                if (this.sourceString != null)
                {
                    Debug.Assert(this.maxPos == this.sourceString.Length);
                    return this.sourceString[index];
                }
                else
                {
                    Debug.Assert(this.maxPos == this.sourceText.Length);
                    return this.sourceText[index];
                }
            else
                return (char)0;
        }
        internal Identifier GetIdentifier()
        {
            string manglingPrefix = this.disableNameMangling ? string.Empty : "___";
            string name = null;
            if (this.identifier.Length > 0)
            {
                name = this.identifier.ToString();
            }
            else
            {
                int start = this.startPos;
                if (this.GetChar(start) == '@') start++;
                int len = this.endPos-start;
                if (this.sourceText != null && len <= 500)
                {
                    Identifier id = new Identifier(manglingPrefix + this.sourceText.Substring(start, len));
                    id.SourceContext = this.CurrentSourceContext;
                    return id;
                }
                name = this.Substring(start, this.endPos-start);
            }
            if (name.Length > 500) //The EE sometimes gets into trouble if presented with a name > 1023 bytes, make this less likely
                name = name.Substring(0, 500) + name.GetHashCode().ToString(CultureInfo.InvariantCulture);
            Identifier identifier =  new Identifier(manglingPrefix + name);
            identifier.SourceContext = this.CurrentSourceContext;
            return identifier;
        }
        internal string GetIdentifierString()
        {
            if (this.identifier.Length > 0) return this.identifier.ToString();
            int start = this.startPos;
            if (this.GetChar(start) == '@') start++;
            return this.Substring(start, this.endPos-this.startPos);
        }
        internal string GetString()
        {
            return this.unescapedString;
        }
        internal Literal GetStringLiteral()
        {
            return new Literal(this.unescapedString, SystemTypes.String, this.CurrentSourceContext);
        }
        internal string GetTokenSource()
        {
            return this.Substring(this.startPos, this.endPos-this.startPos);
        }
        private void ScanCharacter()
        {
            this.ScanString('\'');
            int n = this.unescapedString == null ? 0 : this.unescapedString.Length;
            if (n == 0)
            {
                if (this.GetChar(this.endPos) == '\'')
                {
                    this.charLiteralValue = '\'';
                    this.endPos++;
                    this.HandleError(Error.UnescapedSingleQuote);
                }
                else
                {
                    this.charLiteralValue = (char)0;
                    this.HandleError(Error.EmptyCharConst);
                }
                return;
            }
            this.charLiteralValue = this.unescapedString[0];
            if (n == 1) return;
            this.HandleError(Error.TooManyCharsInConst);
        }
        private void ScanEscapedChar(StringBuilder sb)
        {
            char ch = this.GetChar(this.endPos);
            if (ch != 'U')
            {
                sb.Append(this.ScanEscapedChar());
                return;
            }
            //Scan 32-bit Unicode character. 
            uint escVal = 0;
            this.endPos++;
            for (int i = 0; i < 8; i++)
            {
                ch = this.GetChar(this.endPos++);
                escVal <<= 4;
                if (Scanner.IsHexDigit(ch))
                    escVal |= (uint)Scanner.GetHexValue(ch);
                else
                {
                    this.HandleError(Error.IllegalEscape);
                    this.endPos--;
                    escVal >>= 4;
                    break;
                }
            }
            if (escVal < 0x10000)
                sb.Append((char)escVal);
            else if (escVal <= 0x10FFFF)
            {
                //Append as surrogate pair of 16-bit characters.
                char ch1 = (char)((escVal - 0x10000) / 0x400 + 0xD800);
                char ch2 = (char)((escVal - 0x10000) % 0x400 + 0xDC00);
                sb.Append(ch1);
                sb.Append(ch2);
            }
            else
            {
                sb.Append((char)escVal);
                this.HandleError(Error.IllegalEscape);
            }
        }
        private char ScanEscapedChar()
        {
            int escVal = 0;
            bool requireFourDigits = false;
            int savedStartPos = this.startPos;
            int errorStartPos = this.endPos-1;
            char ch = this.GetChar(this.endPos++);
            switch(ch)
            {
                default:
                    this.startPos = errorStartPos;
                    this.HandleError(Error.IllegalEscape);
                    this.startPos = savedStartPos;
                    if (ch == 'X') goto case 'x';
                    return (char)0;
                    // Single char escape sequences \b etc
                case 'a': return (char)7;
                case 'b': return (char)8;
                case 't': return (char)9;
                case 'n': return (char)10;
                case 'v': return (char)11;
                case 'f': return (char)12;
                case 'r': return (char)13;
                case '"': return '"';
                case '\'': return '\'';
                case '\\': return '\\';
                case '0': 
                    if (this.endPos >= this.maxPos) goto default;
                    return (char)0;
                    // unicode escape sequence \uHHHH
                case 'u':
                    requireFourDigits = true;
                    goto case 'x';
                    // hexadecimal escape sequence \xH or \xHH or \xHHH or \xHHHH
                case 'x':
                    for (int i = 0; i < 4; i++)
                    {
                        ch = this.GetChar(this.endPos++);
                        escVal <<= 4;
                        if (Scanner.IsHexDigit(ch))
                            escVal |= Scanner.GetHexValue(ch);
                        else
                        {
                            if (i == 0 || requireFourDigits)
                            {
                                this.startPos = errorStartPos;
                                this.HandleError(Error.IllegalEscape);
                                this.startPos = savedStartPos;
                            }
                            this.endPos--;
                            return (char)(escVal>>4);
                        }
                    }
                    return (char)escVal;
            }
        }
        private void ScanIdentifier()
        {
            for(;;)
            {
                char c = this.GetChar(this.endPos);
                if (!this.IsIdentifierPartChar(c))
                    break;
                ++this.endPos;
            }
            if (this.idLastPosOnBuilder > 0)
            {
                this.identifier.Append(this.Substring(this.idLastPosOnBuilder, this.endPos - this.idLastPosOnBuilder));
                this.idLastPosOnBuilder = 0;
                if (this.identifier.Length == 0)
                    this.HandleError(Error.UnexpectedToken);
            }
        }
        private Token ScanKeyword(char ch)
        {
            for(;;)
            {
                char c = this.GetChar(this.endPos);
                if ('a' <= c && c <= 'z')
                {
                    this.endPos++;
                    continue;
                }
                else
                {
                    if (this.IsIdentifierPartChar(c))
                    {
                        this.endPos++;
                        this.ScanIdentifier();
                        return Token.Identifier;
                    }
                    break;
                }
            }
            Keyword keyword =  Scanner.Keywords[ch - 'a'];
            if (keyword == null) return Token.Identifier;
            if (this.sourceString != null)
                return keyword.GetKeyword(this.sourceString, this.startPos, this.endPos);
            else
                return keyword.GetKeyword(this.sourceText, this.startPos, this.endPos);
        }
        private Token ScanNumber(char leadChar)
        {
            Token token = leadChar == '.' ? Token.RealLiteral : Token.IntegerLiteral;
            char c;
            if (leadChar == '0')
            {
                c = this.GetChar(this.endPos);
                if (c == 'x' || c == 'X')
                {
                    if (!Scanner.IsHexDigit(this.GetChar(this.endPos + 1)))
                        return token; //return the 0 as a separate token
                    token = Token.HexLiteral;
                    while (Scanner.IsHexDigit(this.GetChar(++this.endPos)));
                    return token;
                }
            }
            bool alreadyFoundPoint = leadChar == '.';
            bool alreadyFoundExponent = false;
            for (;;)
            {
                c = this.GetChar(this.endPos);
                if (!Scanner.IsDigit(c))
                {
                    if (c == '.')
                    {
                        if (alreadyFoundPoint) break;
                        alreadyFoundPoint = true;
                        token = Token.RealLiteral;
                    }
                    else if (c == 'e' || c == 'E')
                    {
                        if (alreadyFoundExponent) break;
                        alreadyFoundExponent = true;
                        alreadyFoundPoint = true;
                        token = Token.RealLiteral;
                    }
                    else if (c == '+' || c == '-')
                    {
                        char e = this.GetChar(this.endPos - 1);
                        if (e != 'e' && e != 'E') break;
                    }
                    else
                        break;
                }
                this.endPos++;
            }
            c = this.GetChar(this.endPos - 1);
            if (c == '.')
            {
                this.endPos--;
                c = this.GetChar(this.endPos - 1);
                return Token.IntegerLiteral;
            }
            if (c == '+' || c == '-')
            {
                this.endPos--;
                c = this.GetChar(this.endPos - 1);
            }
            if (c == 'e' || c == 'E')
                this.endPos--;
            return token; 
        }
        internal TypeCode ScanNumberSuffix()
        {
            this.startPos = this.endPos;
            char ch = this.GetChar(this.endPos++);
            if (ch == 'u' || ch == 'U')
            {
                char ch2 = this.GetChar(this.endPos++);
                if (ch2 == 'l' || ch2 == 'L') return TypeCode.UInt64;
                this.endPos--;
                return TypeCode.UInt32;
            }
            else if (ch == 'l' || ch == 'L')
            {
                if (ch == 'l') this.HandleError(Error.LowercaseEllSuffix);
                char ch2 = this.GetChar(this.endPos++);
                if (ch2 == 'u' || ch2 == 'U') return TypeCode.UInt64;
                this.endPos--;
                return TypeCode.Int64;
            }
            else if (ch == 'f' || ch == 'F')
                return TypeCode.Single;
            else if (ch == 'd' || ch == 'D')
                return TypeCode.Double;
            else if (ch == 'm' || ch == 'M')
                return TypeCode.Decimal;
            this.endPos--;
            return TypeCode.Empty;
        }
        internal bool ScanNamespaceSeparator()
        {
            if (this.endPos >= this.maxPos-2) return false;
            if (this.GetChar(this.endPos) == ':' && this.IsIdentifierStartChar(this.GetChar(this.endPos+1)))
            {
                this.startPos = this.endPos;
                this.endPos++;
                return true;
            }
            return false;
        }

        private bool ScanPreProcessorDirective(bool stopAtEndOfLine, bool insideExcludedBlock, bool atTopLevel)
        {
            bool exclude = insideExcludedBlock;
            int savedStartPos = this.startPos;
            int i = this.startPos-1;
            while (i > 0 && Scanner.IsBlankSpace(this.GetChar(i)))
            {
                i--;
            }
            if (i > 0 && !this.IsLineTerminator(this.GetChar(i), 0))
            {
                this.HandleError(Error.BadDirectivePlacement);
                goto skipToEndOfLine;
            }
            this.SkipBlanks(); //Check EOL/EOF?
            this.startPos = this.endPos-1;
            this.ScanIdentifier();
            switch (this.GetIdentifierString())
            {
                case "define": 
                    if (insideExcludedBlock) goto skipToEndOfLine;
                    if (stopAtEndOfLine) break;
                    if (!this.allowPPDefinitions)
                    {
                        this.HandleError(Error.PPDefFollowsToken);
                        goto skipToEndOfLine;
                    }
                    this.startPos = this.endPos;
                    char chr = this.SkipBlanks();
                    if (this.IsEndLineOrEOF(chr, 0))
                    {
                        this.HandleError(Error.ExpectedIdentifier);
                        break;
                    }
                    this.identifier.Length = 0;
                    this.endPos--;
                    this.startPos = this.endPos;
                    if (!this.IsIdentifierStartChar(chr))
                        this.HandleError(Error.ExpectedIdentifier);
                    else
                    {
                        this.ScanIdentifier();
                        if (this.PreprocessorDefinedSymbols == null)
                            this.PreprocessorDefinedSymbols = new Hashtable();
                        string s = this.GetIdentifierString();
                        if (s == "true" || s == "false" || !this.IsIdentifierStartChar(s[0]))
                        {
                            this.HandleError(Error.ExpectedIdentifier);
                            goto skipToEndOfLine;
                        }
                        else
                            this.PreprocessorDefinedSymbols[s] = s;
                    }
                    break;
                case "undef":
                    if (insideExcludedBlock) goto skipToEndOfLine;
                    if (stopAtEndOfLine) break;
                    if (!this.allowPPDefinitions)
                    {
                        this.HandleError(Error.PPDefFollowsToken);
                        goto skipToEndOfLine;
                    }
                    this.startPos = this.endPos;
                    chr = this.SkipBlanks();
                    if (this.IsEndLineOrEOF(chr, 0))
                    {
                        this.HandleError(Error.ExpectedIdentifier);
                        break;
                    }
                    this.identifier.Length = 0;
                    this.endPos--;
                    this.startPos = this.endPos;
                    if (!this.IsIdentifierStartChar(chr))
                        this.HandleError(Error.ExpectedIdentifier);
                    else
                    {
                        this.ScanIdentifier();
                        if (this.PreprocessorDefinedSymbols == null)
                        {
                            this.PreprocessorDefinedSymbols = new Hashtable();
                            this.PreprocessorDefinedSymbols["true"] = "true";
                        }
                        string s = this.GetIdentifierString();
                        if (s == "true" || s == "false" || !this.IsIdentifierStartChar(s[0]))
                        {
                            this.HandleError(Error.ExpectedIdentifier);
                            goto skipToEndOfLine;
                        }
                        else
                            this.PreprocessorDefinedSymbols[s] = null;
                    }
                    break;
                case "if":
                    if (insideExcludedBlock)
                    {
                        this.endIfCount++;
                        goto skipToEndOfLine;
                    }
                    if (stopAtEndOfLine) break;
                    char c = (char)0;
                    exclude = !this.ScanPPExpression(ref c);
                    if (!exclude) this.includeCount++;
                    this.endIfCount++;
                    if (this.IsEndLineOrEOF(c, 0)) return exclude;
                    break;
                case "elif":
                    if (insideExcludedBlock && !atTopLevel) goto skipToEndOfLine;
                    if (stopAtEndOfLine) break;
                    if (this.elseCount == this.endIfCount)
                    {
                        this.HandleError(Error.UnexpectedDirective);
                        goto skipToEndOfLine;
                    }
                    c = (char)0;
                    exclude = !this.ScanPPExpression(ref c);
                    if (this.includeCount == this.endIfCount)
                    {
                        exclude = true;
                        break;
                    }
                    if (!exclude) this.includeCount++;
                    if (this.IsEndLineOrEOF(c, 0)) return exclude;
                    break;
                case "else":
                    if (insideExcludedBlock && !atTopLevel) goto skipToEndOfLine;
                    if (stopAtEndOfLine) break;
                    if (this.elseCount == this.endIfCount)
                    {
                        this.HandleError(Error.UnexpectedDirective);
                        goto skipToEndOfLine;
                    }
                    this.elseCount++;
                    if (this.includeCount == this.endIfCount)
                    {
                        exclude = true;
                        break;
                    }
                    exclude = false;
                    this.includeCount++;
                    break;
                case "endif":
                    if (stopAtEndOfLine) break;
                    if (this.endIfCount <= 0)
                    {
                        this.endIfCount = 0;
                        this.HandleError(Error.UnexpectedDirective);
                        goto skipToEndOfLine;
                    }
                    this.elseCount = this.includeCount = --this.endIfCount;
                    break;
                case "line":
                    if (insideExcludedBlock) goto skipToEndOfLine;
                    if (stopAtEndOfLine) break;
                    c = this.SkipBlanks();
                    int lnum = -1;
                    if ('0' <= c && c <= '9')
                    {
                        this.startPos = --this.endPos;
                        while ('0' <= (c = this.GetChar(++this.endPos)) && c <= '9');
                        try
                        {
                            lnum = int.Parse(this.GetTokenSource(), CultureInfo.InvariantCulture);
                            if (lnum <= 0)
                            {
                                this.startPos = this.endPos;
                                this.HandleError(Error.InvalidLineNumber);
                                goto skipToEndOfLine;
                            }
                            else if (this.IsEndLineOrEOF(c, 0))
                                goto setLineInfo;
                        }
                        catch(OverflowException)
                        {
                            this.startPos++;
                            this.HandleError(Error.IntOverflow);
                            goto skipToEndOfLine;
                        }
                    }
                    else
                    {
                        this.startPos = this.endPos-1;
                        this.ScanIdentifier();
                        if (this.startPos != this.endPos-1)
                        {
                            string str = this.GetIdentifierString();
                            if (str == "default")
                            {
                                this.document = this.originalDocument;
                                break;
                            }
                            if (str == "hidden")
                            {
                                this.document = new Document(this.document.Name, this.document.LineNumber, this.document.Text, this.document.DocumentType, this.document.Language, this.document.LanguageVendor);
                                this.document.Hidden = true;
                                break;
                            }
                        }
                        this.HandleError(Error.InvalidLineNumber);
                        goto skipToEndOfLine;
                    }
                    c = this.SkipBlanks();
                    this.startPos = this.endPos-1;
                    if (c == '/')
                    {
                        if (this.GetChar(this.endPos) == '/')
                        {
                            this.endPos--;
                            goto setLineInfo;
                        }
                        else
                        {
                            this.startPos = this.endPos-1;
                            this.HandleError(Error.EndOfPPLineExpected);
                            goto skipToEndOfLine;
                        }
                    }
                    if (c == '"')
                    {
                        while ((c = this.GetChar(this.endPos++)) != '"' && !this.IsEndLineOrEOF(c, 0));
                        if (c != '"')
                        {
                            this.HandleError(Error.MissingPPFile);
                            goto skipToEndOfLine;
                        }
                        this.startPos++;
                        this.endPos--;
                        string filename = this.GetTokenSource();
                        this.endPos++;
                        this.document = new Document(filename, 1, this.document.Text, this.document.DocumentType, this.document.Language, this.document.LanguageVendor);
                    }
                    else if (!this.IsEndLineOrEOF(c, 0))
                    {
                        this.HandleError(Error.MissingPPFile);
                        goto skipToEndOfLine;
                    }
                    setLineInfo:
                        this.document = new Document(this.document.Name, 1, this.document.Text, this.document.DocumentType, this.document.Language, this.document.LanguageVendor);
                    int offset = lnum - this.document.GetLine(this.startPos);
                    this.document.LineNumber = offset;
                    if (this.IsEndLineOrEOF(c, 0)) return exclude;
                    break;
                case "error":
                    if (insideExcludedBlock) goto skipToEndOfLine;
                    if (stopAtEndOfLine) break;
                    this.SkipBlanks();
                    this.startPos = --this.endPos;
                    this.ScanString((char)0);
                    this.HandleError(Error.ErrorDirective, this.unescapedString);
                    break;
                case "warning":
                    if (insideExcludedBlock) goto skipToEndOfLine;
                    if (stopAtEndOfLine) break;
                    this.SkipBlanks();
                    this.startPos = --this.endPos;
                    this.ScanString((char)0);
                    this.HandleError(Error.WarningDirective, this.unescapedString);
                    break;
                case "region":
                    if (insideExcludedBlock) goto skipToEndOfLine;
                    if (stopAtEndOfLine) break;
                    this.endRegionCount++;
                    goto skipToEndOfLine;
                case "endregion":
                    if (insideExcludedBlock) goto skipToEndOfLine;
                    if (stopAtEndOfLine) break;
                    if (this.endRegionCount <= 0)
                        this.HandleError(Error.UnexpectedDirective);
                    else
                        this.endRegionCount--;
                    goto skipToEndOfLine;
                default:
                    if (insideExcludedBlock) goto skipToEndOfLine;
                    if (stopAtEndOfLine)
                    {
                        this.endPos = this.startPos;
                        break;
                    }
                    this.HandleError(Error.PPDirectiveExpected);
                    goto skipToEndOfLine;
            }
            if (stopAtEndOfLine)
            {
                this.startPos = savedStartPos;
                return false;
            }
            char ch = this.SkipBlanks();
            if (this.IsEndLineOrEOF(ch, 0)) return exclude;
            if (ch == '/' && (ch = this.GetChar(this.endPos++)) == '/') goto skipToEndOfLine;
            this.startPos = this.endPos-1;
            this.HandleError(Error.EndOfPPLineExpected);
            skipToEndOfLine:
                this.SkipSingleLineComment();
            return exclude;
        }
        private bool ScanPPExpression(ref char c)
        {
            c = this.SkipBlanks();
            this.startPos = this.endPos-1;
            if (this.IsEndLineOrEOF(c, 0))
            {
                if (c == 0x0A && this.startPos > 0) this.startPos--;
                this.HandleError(Error.InvalidPreprocExpr);
                c = ')';
                return true;
            }
            bool result = this.ScanPPOrExpression(ref c);
            if (c == '/' && this.GetChar(this.endPos) == '/')
            {
                this.SkipSingleLineComment();
                c = (char)0x0a;
            }
            return result;
        }
        private bool ScanPPOrExpression(ref char c)
        {
            bool result = this.ScanPPAndExpression(ref c);
            while (c == '|')
            {
                char c2 = this.GetChar(this.endPos++);
                if (c2 == '|')
                {
                    c = this.SkipBlanks();
                    bool opnd2 = this.ScanPPAndExpression(ref c);
                    result = result || opnd2;          
                }
                else
                {
                    this.startPos = this.endPos-2;
                    this.HandleError(Error.InvalidPreprocExpr);
                    this.SkipSingleLineComment();
                    c = (char)0x0A;
                    return true;
                }
            }
            return result;
        }
        private bool ScanPPAndExpression(ref char c)
        {
            bool result = this.ScanPPEqualityExpression(ref c);
            while (c == '&')
            {
                char c2 = this.GetChar(this.endPos++);
                if (c2 == '&')
                {
                    c = this.SkipBlanks();
                    bool opnd2 = this.ScanPPEqualityExpression(ref c);
                    result = result && opnd2;          
                }
                else
                {
                    this.startPos = this.endPos-2;
                    this.HandleError(Error.InvalidPreprocExpr);
                    this.SkipSingleLineComment();
                    c = (char)0x0A;
                    return true;
                }
            }
            return result;
        }
        private bool ScanPPEqualityExpression(ref char c)
        {
            bool result = this.ScanPPUnaryExpression(ref c);
            while (c == '=' || c == '!')
            {
                char c2 = this.GetChar(this.endPos++);
                if (c == '=' && c2 == '=')
                {
                    c = this.SkipBlanks();
                    bool opnd2 = this.ScanPPUnaryExpression(ref c);
                    result = result == opnd2;          
                }
                else if (c == '!' && c2 == '=')
                {
                    c = this.SkipBlanks();
                    bool opnd2 = this.ScanPPUnaryExpression(ref c);
                    result = result != opnd2;
                }
                else
                {
                    this.startPos = this.endPos-2;
                    this.HandleError(Error.InvalidPreprocExpr);
                    this.SkipSingleLineComment();
                    c = (char)0x0A;
                    return true;
                }
            }
            return result;
        }
        private bool ScanPPUnaryExpression(ref char c)
        {
            if (c == '!')
            {
                c = this.SkipBlanks();
                return !this.ScanPPUnaryExpression(ref c);
            }
            return this.ScanPPPrimaryExpression(ref c);
        }
        private bool ScanPPPrimaryExpression(ref char c)
        {
            bool result = true;
            if (c == '(')
            {
                result = this.ScanPPExpression(ref c);
                if (c != ')')
                    this.HandleError(Error.ExpectedRightParenthesis);
                c = this.SkipBlanks();
                return result;
            }
            this.startPos = this.endPos-1;
            this.ScanIdentifier();
            if (this.endPos > this.startPos)
            {
                string id = this.GetIdentifierString();
                string sym = (string) this.PreprocessorDefinedSymbols[id];
                if (id == null || id.Length == 0 || !this.IsIdentifierStartChar(id[0]))
                    this.HandleError(Error.ExpectedIdentifier);
                result = sym != null;
                c = this.SkipBlanks();
            }
            else
                this.HandleError(Error.ExpectedIdentifier);
            return result;
        }

        private void ScanString(char closingQuote)
        {
            char ch;
            int start = this.endPos;
            this.unescapedString = null;
            StringBuilder unescapedSB = null;
            do
            {
                ch = this.GetChar(this.endPos++);
                if (ch == '\\')
                {
                    // Got an escape of some sort. Have to use the StringBuilder
                    if (unescapedSB == null) unescapedSB = new StringBuilder(128);
                    // start points to the first position that has not been written to the StringBuilder.
                    // The first time we get in here that position is the beginning of the string, after that
                    // it is the character immediately following the escape sequence
                    int len = this.endPos - start - 1;
                    if (len > 0) // append all the non escaped chars to the string builder
                        if (this.sourceString != null)
                            unescapedSB.Append(this.sourceString, start, len);
                        else
                            unescapedSB.Append(this.sourceText.Substring(start, len));          
                    int savedEndPos = this.endPos-1;
                    this.ScanEscapedChar(unescapedSB); //might be a 32-bit unicode character
                    if (closingQuote == (char)0 && unescapedSB.Length > 0 && unescapedSB[unescapedSB.Length-1] == (char)0)
                    {
                        unescapedSB.Length -= 1;
                        this.endPos = savedEndPos;
                        start = this.endPos;
                        break;
                    }
                    start = this.endPos;
                }
                else
                {
                    // This is the common non escaped case
                    if (this.IsLineTerminator(ch, 0) || (ch == 0 && this.endPos >= this.maxPos))
                    {
                        this.FindGoodRecoveryPoint(closingQuote);
                        break;
                    }
                }
            }while (ch != closingQuote);
            // update this.unescapedString using the StringBuilder
            if (unescapedSB != null)
            {
                int len = this.endPos - start - 1;
                if (len > 0)
                {
                    // append all the non escape chars to the string builder
                    if (this.sourceString != null)
                        unescapedSB.Append(this.sourceString, start, len);
                    else
                        unescapedSB.Append(this.sourceText.Substring(start, len));
                }
                this.unescapedString = unescapedSB.ToString();
            }
            else
            {
                if (closingQuote == (char)0)
                    this.unescapedString = this.Substring(this.startPos, this.endPos - this.startPos);
                else if (closingQuote == '\'' && (this.startPos == this.endPos-1 || this.GetChar(this.endPos-1) != '\''))
                    this.unescapedString = this.Substring(this.startPos + 1, 1); //suppres further errors
                else if (this.endPos <= this.startPos + 2)
                    this.unescapedString = "";
                else
                    this.unescapedString = this.Substring(this.startPos + 1, this.endPos - this.startPos - 2);
            }
        }
        private void FindGoodRecoveryPoint(char closingQuote)
        {
            if (closingQuote == (char)0)
            {
                //Scan backwards to last char before new line or EOF
                if (this.endPos >= this.maxPos)
                {
                    this.endPos = this.maxPos; return;
                }
                char ch = this.GetChar(this.endPos-1);
                while (Scanner.IsEndOfLine(ch))
                {
                    this.endPos--;
                    ch = this.GetChar(this.endPos-1);
                }
                return;
            }
            int endPos = this.endPos;
            int i;
            int maxPos = this.maxPos;
            if (endPos < maxPos)
            {
                //scan forward in next line looking for suitable matching quote
                for (i = endPos; i < maxPos; i++)
                {
                    char ch = this.GetChar(i);
                    if (ch == closingQuote)
                    {
                        //Give an error, but go on as if new line is allowed
                        this.endPos--;
                        if (this.GetChar(this.endPos-1) == (char)0x0d) this.endPos--;
                        this.HandleError(Error.NewlineInConst);
                        this.endPos = i+1;
                        return;
                    }
                    switch (ch)
                    {
                        case ';':
                        case '}':
                        case ')':
                        case ']':
                        case '(':
                        case '[':
                        case '+':
                        case '-':
                        case '*':
                        case '/':
                        case '%':
                        case '!':
                        case '=':
                        case '<':
                        case '>':
                        case '|':
                        case '&':
                        case '^':
                        case '@':
                        case ',':
                        case '"':
                        case '\'':
                            i = maxPos; break;
                    }
                }
            }
            else
                this.endPos = endPos = this.maxPos;
            int lastSemicolon = endPos;
            int lastNonBlank = this.startPos;
            for (i = this.startPos; i < endPos; i++)
            {
                char ch = this.GetChar(i);
                if (ch == ';') {lastSemicolon = i; lastNonBlank = i;}
                if (ch == '/' && i < endPos-1)
                {
                    char ch2 = this.GetChar(++i);
                    if (ch2 == '/' || ch2 == '*')
                    {
                        i -= 2; break;
                    }
                }
                if (Scanner.IsEndOfLine(ch)) break;
                if (!Scanner.IsBlankSpace(ch)) lastNonBlank = i;
            }
            if (lastSemicolon == lastNonBlank)
                this.endPos = lastSemicolon;
            else
                this.endPos = i;
            int savedStartPos = this.startPos;
            this.startPos = this.endPos;
            this.endPos++;
            if (closingQuote == '"')
                this.HandleError(Error.ExpectedDoubleQuote);
            else
                this.HandleError(Error.ExpectedSingleQuote);
            this.startPos = savedStartPos;
            this.endPos--;
        }
        private void ScanVerbatimString(bool stopAtEndOfLine)
        {
            char ch;
            int start = this.endPos;
            this.unescapedString = null;
            StringBuilder unescapedSB = null;
            for(;;)
            {
                ch = this.GetChar(this.endPos++);
                if (ch == '"')
                {
                    ch = this.GetChar(this.endPos);
                    if (ch != '"') break; //Reached the end of the string
                    this.endPos++;
                    if (unescapedSB == null) unescapedSB = new StringBuilder(128);
                    // start points to the first position that has not been written to the StringBuilder.
                    // The first time we get in here that position is the beginning of the string, after that
                    // it is the character immediately following the "" pair
                    int len = this.endPos - start;
                    if (len > 0) // append all the non escaped chars to the string builder
                        if (this.sourceString != null)
                            unescapedSB.Append(this.sourceString, start, len);
                        else
                            unescapedSB.Append(this.sourceText.Substring(start, len));
                    start = this.endPos;
                }
                else if (this.IsLineTerminator(ch, 1))
                {
                    ch = this.GetChar(++this.endPos);
                    if (stopAtEndOfLine)
                    {
                        this.stillInsideMultiLineToken = true;
                        return;
                    }
                }
                else if (ch == (char)0 && this.endPos >= this.maxPos)
                {
                    //Reached EOF
                    this.endPos--;
                    this.HandleError(Error.NewlineInConst);
                    break;
                }
            }
            // update this.unescapedString using the StringBuilder
            if (unescapedSB != null)
            {
                int len = this.endPos - start - 1;
                if (len > 0)
                {
                    // append all the non escape chars to the string builder
                    if (this.sourceString != null)
                        unescapedSB.Append(this.sourceString, start, len);
                    else
                        unescapedSB.Append(this.sourceText.Substring(start, len));
                }
                this.unescapedString = unescapedSB.ToString();
            }
            else
            {
                if (this.endPos <= this.startPos + 3)
                    this.unescapedString = "";
                else
                    this.unescapedString = this.Substring(this.startPos + 2, this.endPos - this.startPos - 3);
            }
        }
        private void SkipSingleLineComment()
        {
            while(!this.IsEndLineOrEOF(this.GetChar(this.endPos++), 0));
        }
        private void SkipMultiLineComment(bool stopAtEndOfLine)
        {
            for(;;)
            {
                char c = this.GetChar(this.endPos);
                while (c == '*')
                {
                    c = this.GetChar(++this.endPos);
                    if (c == '/')
                    {
                        this.endPos++;
                        return;
                    }
                    else if (c == (char)0 && this.endPos >= this.maxPos)
                        return;
                    else if (this.IsLineTerminator(c, 1))
                    {
                        c = this.GetChar(++this.endPos);
                        if (stopAtEndOfLine)
                        {
                            this.stillInsideMultiLineToken = true;
                            return;
                        }
                    }
                }
                if (c == (char)0 && this.endPos >= this.maxPos) return;
                if (stopAtEndOfLine && this.IsLineTerminator(c, 1))
                {
                    this.endPos++;
                    this.stillInsideMultiLineToken = true;
                    return;
                }
                ++this.endPos;
            }
        }
        private char SkipBlanks()
        {
            char c = this.GetChar(this.endPos);
            while(Scanner.IsBlankSpace(c) ||
                (c == (char)0 && this.endPos < this.maxPos))
            { // silently skip over nulls
                c = this.GetChar(++this.endPos);
            }
            if (c != '\0') this.endPos++;
            return c;
        }
        private static bool IsBlankSpace(char c)
        {
            switch (c)
            {
                case (char)0x09:
                case (char)0x0B:
                case (char)0x0C:
                case (char)0x1A:
                case (char)0x20:
                    return true;
                default:
                    if (c >= 128)
                        return Char.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator;
                    else
                        return false;
            }
        }
        private static bool IsEndOfLine(char c)
        {
            switch (c)
            {
                case (char)0x0D:
                case (char)0x0A:
                case (char)0x2028:
                case (char)0x2029:
                    return true;
                default:
                    return false;
            }
        }
        private bool IsLineTerminator(char c, int increment)
        {
            switch (c)
            {
                case (char)0x0D:
                    // treat 0x0D0x0A as a single character
                    if (this.GetChar(this.endPos + increment) == 0x0A)
                        this.endPos++;
                    return true;
                case (char)0x0A:
                    return true;
                case (char)0x2028:
                    return true;
                case (char)0x2029:
                    return true;
                default:
                    return false;
            }
        }
        private bool IsEndLineOrEOF(char c, int increment)
        {
            return this.IsLineTerminator(c, increment) || c == (char)0 && this.endPos >= this.maxPos;
        }
        internal bool IsIdentifierPartChar(char c)
        {
            if (this.IsIdentifierStartCharHelper(c, true))
                return true;
            if ('0' <= c && c <= '9')
                return true;
            if (c == '\\')
            {
                this.endPos++;
                this.ScanEscapedChar();
                this.endPos--;
                return true; //It is not actually true, or IsIdentifierStartCharHelper would have caught it, but this makes for better error recovery
            }
            return false;
        }
        internal bool IsIdentifierStartChar(char c)
        {
            return this.IsIdentifierStartCharHelper(c, false);
        }
        private bool IsIdentifierStartCharHelper(char c, bool expandedUnicode)
        {
            bool isEscapeChar = false;
            int escapeLength = 0;
            UnicodeCategory ccat = 0;
            if (c == '\\')
            {
                isEscapeChar = true;
                char cc = this.GetChar(this.endPos + 1);
                switch (cc)
                {
                    case '-':
                        c = '-';
                        goto isIdentifierChar;
                    case 'u':
                        escapeLength = 4; 
                        break;
                    case 'U':
                        escapeLength = 8; 
                        break;
                    default:
                        return false;
                }
                int escVal = 0;
                for (int i = 0; i < escapeLength; i++)
                {
                    char ch = this.GetChar(this.endPos + 2 + i);
                    escVal <<= 4;
                    if (Scanner.IsHexDigit(ch))
                        escVal |= Scanner.GetHexValue(ch);
                    else
                    {
                        escVal >>= 4;
                        break;
                    }
                }
                if (escVal > 0xFFFF) return false; //REVIEW: can a 32-bit Unicode char ever be legal? If so, how does one categorize it?
                c = (char)escVal;
            }
            if ('a' <= c && c <= 'z' || 'A' <= c && c <= 'Z' || c == '_' || c == '$')
                goto isIdentifierChar;
            if (c < 128)
                return false;
            ccat = Char.GetUnicodeCategory(c);
            switch (ccat)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber:
                    goto isIdentifierChar;
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.ConnectorPunctuation:
                    if (expandedUnicode) goto isIdentifierChar;
                    return false;
                case UnicodeCategory.Format:
                    if (expandedUnicode)
                    {
                        if (!isEscapeChar)
                        {
                            isEscapeChar = true;
                            escapeLength = -1;
                        }
                        goto isIdentifierChar;
                    }
                    return false;
                default:
                    return false;
            }
            isIdentifierChar:
                if (isEscapeChar)
                {
                    int startPos = this.idLastPosOnBuilder;
                    if (startPos == 0) startPos = this.startPos;
                    if (this.endPos > startPos)
                        this.identifier.Append(this.Substring(startPos, this.endPos - startPos));
                    if (ccat != UnicodeCategory.Format)
                        this.identifier.Append(c);
                    this.endPos += escapeLength + 1;
                    this.idLastPosOnBuilder = this.endPos + 1;
                }
            return true;
        }
        internal static bool IsDigit(char c)
        {
            return '0' <= c && c <= '9';
        }
        internal static bool IsHexDigit(char c)
        {
            return Scanner.IsDigit(c) || 'A' <= c && c <= 'F' || 'a' <= c && c <= 'f';
        }
        internal static bool IsAsciiLetter(char c)
        {
            return 'A' <= c && c <= 'Z' || 'a' <= c && c <= 'z';
        }
        internal static bool IsUnicodeLetter(char c)
        {
            return c >= 128 && Char.IsLetter(c);
        }
        private void HandleError(Error error, params string[] messageParameters)
        {
            if (this.errors == null) return;
            if (this.endPos <= this.lastReportedErrorPos) return;
            this.lastReportedErrorPos = this.endPos;
            ErrorNode enode = new ZingErrorNode(error, messageParameters);
            enode.SourceContext = new SourceContext(this.document, this.startPos, this.endPos);
            this.errors.Add(enode);
        }
        private static int GetHexValue(char hex)
        {
            int hexValue;
            if ('0' <= hex && hex <= '9')
                hexValue = hex - '0';
            else if ('a' <= hex && hex <= 'f')
                hexValue = hex - 'a' + 10;
            else
                hexValue = hex - 'A' + 10;
            return hexValue;
        }

        internal SourceContext CurrentSourceContext
        {
            get{return new SourceContext(this.document, this.startPos, this.endPos);}
        }
    }
    public enum Token : int
    {
        None,

        // Keywords
        Activate,
        Array,
        Accept,
        Assert,
        Assume,
        Async,
        Atomic,
        Bool,
        Byte,
        Chan,
        Choose,
        Class,
        Decimal,
        Double,
        Else,
        Enum,
        End,
        Event,
        False,
        First,
        Float,
        Foreach,
        Goto,
        If,
        In,
        Int,
        Interface,
        InvokePlugin,
        InvokeShed,
        Long,
        New,
        Null,
        Object,
        Out,
        Range,
        Raise,
        Receive,
        Return,
        SByte,
        Select,
        Send,
        Set,
        Self,
        Short,
        Sizeof,
        Static,
        Struct,
        String,
        This,
        Timeout,
        Trace,
        True,
        Try,
        Typeof,
        UInt,
        ULong,
        UShort,
        Visible,
        Void,
        Wait,
        While,
        With,
        Yield,

        // Literals
        HexLiteral,
        Identifier,
        IntegerLiteral,
        StringLiteral,
        RealLiteral,        // scanner accepts this, but parser will reject it

        // Punctuation
        Comma,
        Semicolon,
        LeftParenthesis,
        RightParenthesis,
        LeftBrace,
        RightBrace,
        LeftBracket,
        RightBracket,
        Assign,
        Colon,
        Dot,
        DotDot,
        Remainder,
        Plus,
        Subtract,
        Multiply,
        Divide,
        LogicalNot,
        BitwiseAnd,
        BitwiseNot,
        BitwiseOr,
        BitwiseXor,
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        LogicalAnd,
        LogicalOr,
        LeftShift,
        RightShift,
        Arrow,

        // Misc
        IllegalCharacter,
        MultiLineComment,
        SingleLineComment,
        EndOfLine,
        EndOfFile,
    }
    internal sealed class Keyword
    {
        private Keyword next;
        private Token token;
        private string name;
        private int length;

        private Keyword(Token token, string name)
        {
            this.name = name;
            this.next = null;
            this.token = token;
            this.length = this.name.Length;
        }

        private Keyword(Token token, string name, Keyword next)
        {
            this.name = name;
            this.next = next;
            this.token = token;
            this.length = this.name.Length;
        }

        internal Token GetKeyword(string source, int startPos, int endPos)
        {
            int length = endPos - startPos;
            Keyword keyword = this;
            nextToken:
                while (null != keyword)
                {
                    if (length == keyword.length)
                    {
                        // we know the first char has to match
                        string name = keyword.name;
                        for (int i = 1, j = startPos+1; i < length; i++, j++)
                        {
                            char ch1 = name[i];
                            char ch2 = source[j];
                            if (ch1 == ch2)
                                continue;
                            else if (ch2 < ch1)
                                return Token.Identifier;
                            else
                            {
                                keyword = keyword.next;
                                goto nextToken;
                            }
                        }
                        return keyword.token;
                    }
                    else if (length < keyword.length)
                        return Token.Identifier;

                    keyword = keyword.next;
                }
            return Token.Identifier;
        }

        internal Token GetKeyword(DocumentText source, int startPos, int endPos)
        {
            int length = endPos - startPos;
            Keyword keyword = this;
            nextToken:
                while (null != keyword)
                {
                    if (length == keyword.length)
                    {
                        // we know the first char has to match
                        string name = keyword.name;
                        for (int i = 1, j = startPos+1; i < length; i++, j++)
                        {
                            char ch1 = name[i];
                            char ch2 = source[j];
                            if (ch1 == ch2)
                                continue;
                            else if (ch2 < ch1)
                                return Token.Identifier;
                            else
                            {
                                keyword = keyword.next;
                                goto nextToken;
                            }
                        }
                        return keyword.token;
                    }
                    else if (length < keyword.length)
                        return Token.Identifier;

                    keyword = keyword.next;
                }
            return Token.Identifier;
        }

        internal static Keyword[] InitKeywords()
        {
            // you'll have to add the longer keywords first!

            Keyword[] keywords = new Keyword[26];
            Keyword keyword;
            // a
            keyword = new Keyword(Token.Activate, "activate");
            keyword = new Keyword(Token.Atomic, "atomic", keyword);
            keyword = new Keyword(Token.Assume, "assume", keyword);
            keyword = new Keyword(Token.Assert, "assert", keyword);
            keyword = new Keyword(Token.Accept, "accept", keyword);
            keyword = new Keyword(Token.Async, "async", keyword);
            keyword = new Keyword(Token.Array, "array", keyword);
            keywords['a' - 'a'] = keyword;
            // b
            keyword = new Keyword(Token.Byte, "byte");
            keyword = new Keyword(Token.Bool, "bool", keyword);
            keywords['b' - 'a'] = keyword;
            // c
            keyword = new Keyword(Token.Choose, "choose");
            keyword = new Keyword(Token.Class, "class", keyword);
            keyword = new Keyword(Token.Chan, "chan", keyword);
            keywords['c' - 'a'] = keyword;
            // d
            keyword = new Keyword(Token.Decimal, "decimal");
            keyword = new Keyword(Token.Double, "double", keyword);
            keywords['d' - 'a'] = keyword;
            // e
            keyword = new Keyword(Token.Event, "event", keyword);
            keyword = new Keyword(Token.Enum, "enum", keyword);
            keyword = new Keyword(Token.Else, "else", keyword);
            keyword = new Keyword(Token.End, "end", keyword);
            keywords['e' - 'a'] = keyword;
            // f
            keyword = new Keyword(Token.Foreach, "foreach");
            keyword = new Keyword(Token.Float, "float", keyword);
            keyword = new Keyword(Token.First, "first", keyword);
            keyword = new Keyword(Token.False, "false", keyword);
            keywords['f' - 'a'] = keyword;
            // g
            keyword = new Keyword(Token.Goto, "goto");
            keywords['g' - 'a'] = keyword;
            // i
            keyword = new Keyword(Token.InvokeShed, "invokescheduler");
            keyword = new Keyword(Token.InvokePlugin, "invokeplugin", keyword);
            keyword = new Keyword(Token.Interface, "interface", keyword);
            keyword = new Keyword(Token.Int, "int", keyword);
            keyword = new Keyword(Token.In, "in", keyword);
            keyword = new Keyword(Token.If, "if", keyword);
            keywords['i' - 'a'] = keyword;
            // l
            keyword = new Keyword(Token.Long, "long");
            keywords['l' - 'a'] = keyword;
            // n
            keyword = new Keyword(Token.Null, "null");
            keyword = new Keyword(Token.New, "new", keyword); 
            keywords['n' - 'a'] = keyword;
            // o
            keyword = new Keyword(Token.Object, "object");
            keyword = new Keyword(Token.Out, "out", keyword);
            keywords['o' - 'a'] = keyword;

            // r
            keyword = new Keyword(Token.Receive, "receive");
            keyword = new Keyword(Token.Return, "return", keyword);
            keyword = new Keyword(Token.Range, "range", keyword);
            keyword = new Keyword(Token.Raise, "raise", keyword);
            keywords['r' - 'a'] = keyword;
            
            // s
            keyword = new Keyword(Token.Struct, "struct", keyword);
            keyword = new Keyword(Token.String, "string", keyword);
            keyword = new Keyword(Token.Static, "static", keyword);
            keyword = new Keyword(Token.Sizeof, "sizeof", keyword);
            keyword = new Keyword(Token.Select, "select", keyword);
            keyword = new Keyword(Token.Short, "short", keyword);
            keyword = new Keyword(Token.SByte, "sbyte", keyword);
            keyword = new Keyword(Token.Send, "send", keyword);
            keyword = new Keyword(Token.Self, "self", keyword);
            keyword = new Keyword(Token.Set, "set", keyword);

            keywords['s' - 'a'] = keyword;
            // t
            keyword = new Keyword(Token.Timeout, "timeout");
            keyword = new Keyword(Token.Typeof, "typeof", keyword);
            keyword = new Keyword(Token.Trace, "trace", keyword);
            keyword = new Keyword(Token.True, "true", keyword);
            keyword = new Keyword(Token.This, "this", keyword);
            keyword = new Keyword(Token.Try, "try", keyword);
            keywords['t' - 'a'] = keyword;
            // u
            keyword = new Keyword(Token.UShort, "ushort");
            keyword = new Keyword(Token.ULong, "ulong", keyword);
            keyword = new Keyword(Token.UInt, "uint", keyword);
            keywords['u' - 'a'] = keyword;
            // v
            keyword = new Keyword(Token.Visible, "visible");
            keyword = new Keyword(Token.Void, "void", keyword);
            keywords['v' - 'a'] = keyword;
            // w
            keyword = new Keyword(Token.While, "while");
            keyword = new Keyword(Token.With, "with", keyword);
            keyword = new Keyword(Token.Wait, "wait", keyword);
            keywords['w' - 'a'] = keyword;
            // y
            keyword = new Keyword(Token.Yield, "yield");
            keywords['y' - 'a'] = keyword;

            return keywords;
        }
    }
}
