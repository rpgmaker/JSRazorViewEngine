using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections.Concurrent;
using JSRazorViewEngine.Interfaces;

namespace JSRazorViewEngine.Runtime {
    [Flags]
    internal enum BlockType {
        None = 0,
        Code = 50,
        Expression = 2,
        For = (Code << 2) | 3,
        If = (Code << 2) | 4,
        While = (Code << 2) | 5,
        DoWhile = (Code << 2) | 6,
        Switch = (Code << 2) | 7,
        With = (Code << 2) | 8,
        Helper = (Code << 2) | 9,
        ExplicitExpression = 3,
        Comment = 4,
        Text = 5
    }

    internal unsafe sealed class RazorDocumentParser : IDocumentParser {

        private RazorParser _parser;
        private StringBuilder _buffer;
        private BlockType _blockType = BlockType.None;
        private int _bcounter = -1,
            _ecounter = -1, _razorIndex = 0, _razorCounter = -1;

        private char _charBeforeRazor = RazorParser.Null;
        private RazorDocumentParser _root;
        private HtmlDocumentParser _htmlParser;

        private int? _bbcount;
       

        public RazorDocumentParser() {
            _buffer = new StringBuilder();
        }

        public RazorDocumentParser(RazorDocumentParser rootParser) : this(){
            _root = rootParser;
        }

        public RazorDocumentParser(HtmlDocumentParser htmlParser)
            : this() {
            _htmlParser = htmlParser;
        }

        public RazorDocumentParser(RazorDocumentParser razorParser, HtmlDocumentParser htmlParser)
            : this() {
            _root = razorParser;
            _htmlParser = htmlParser;
        }

        void IDocumentParser.Reset() {

        }

        void IDocumentParser.Execute(RazorParser parser) {
            _parser = parser;
            Process();
        }

        private void Process() {
            _razorIndex = _parser.charIndex;
            _charBeforeRazor = _parser.GetCharacterAtIndex(_razorIndex - 1);
            _razorCounter = 1;
            var nextChar = _parser.NextChar();
            while (nextChar == RazorParser.RazorChr) {
                _razorCounter++;
                _razorIndex = _parser.charIndex++;
                nextChar = _parser.NextChar();
            }

            if (_razorCounter > 1) {
                var razorDisplayCount = (int)Math.Floor(_razorCounter / 2.0);
                _parser.WriteHtml(String.Join(string.Empty, Enumerable.Range(0, razorDisplayCount).Select(x => RazorParser.RazorChr)));
                if (_razorCounter % 2 == 0) {
                    return;
                } else {
                    _charBeforeRazor = _parser.GetCharacterAtIndex(_razorIndex - 1);
                }
            }

            if(_parser.IsTerminateChar(nextChar))
                throw new InvalidOperationException("Invalid operation detected. Razor syntax is incomplete");
            if (nextChar == RazorParser.ExplicitBlockStart) {
                _blockType = BlockType.ExplicitExpression;
                _parser.charIndex += 2;
                _ecounter = 1;
            } else if (nextChar == RazorParser.TextChr) {
                _blockType = BlockType.Text;
                _parser.charIndex += 2;
            } else if (nextChar == RazorParser.CommentChar) {
                _parser.charIndex += 2;
                _blockType = BlockType.Comment;
                if (_root != null)
                    _parser.WritePlainCode("/*");
                else {
                    _parser.WritePlainHtml("<!--");
                }
            } else if (nextChar == RazorParser.BlockStartBrace) {
                _blockType = BlockType.Code;
                _parser.charIndex += 2;
                _bcounter = 1;
            } else {
                _parser.charIndex++;
                var index = _parser.charIndex;
                int operandIfIndex = _parser.FindNextChars(index, "if("),
                                    operandWithIndex = _parser.FindNextChars(index, "with("),
                                    operandForIndex = _parser.FindNextChars(index, "for("),
                                    operandWhileIndex = _parser.FindNextChars(index, "while("),
                                    operandSwitchIndex = _parser.FindNextChars(index, "switch("),
                                    operandHelperIndex = _parser.FindNextChars(index, "helper "),
                                    operandDoWhileIndex = _parser.FindNextChars(index, "do{");

                if (operandIfIndex > -1) {
                    _blockType = BlockType.If;
                    _parser.WritePlainCode("if(");
                    _parser.charIndex = operandIfIndex;
                } else if (operandHelperIndex > -1) {
                    _blockType = BlockType.Helper;
                    _parser.helper = true;
                    _parser.WritePlainCode("function ");
                    _parser.charIndex = operandHelperIndex;
                } else if (operandForIndex > -1) {
                    _blockType = BlockType.For;
                    _parser.WritePlainCode("for(");
                    _parser.charIndex = operandForIndex;
                } else if (operandWhileIndex > -1) {
                    _blockType = BlockType.While;
                    _parser.WritePlainCode("while(");
                    _parser.charIndex = operandWhileIndex;
                } else if (operandWithIndex > -1) {
                    _blockType = BlockType.With;
                    _parser.WritePlainCode("with(");
                    _parser.charIndex = operandWithIndex;
                } else if (operandSwitchIndex > -1) {
                    _blockType = BlockType.Switch;
                    _parser.WritePlainCode("switch(");
                    _parser.charIndex = operandSwitchIndex;
                } else if (operandDoWhileIndex > -1) {
                    _blockType = BlockType.DoWhile;
                    _parser.WritePlainCode("do{");
                    _parser.charIndex = operandDoWhileIndex;
                    _bcounter = 1;
                } else {
                    _blockType = BlockType.Expression;
                }
            }
            Parse();
        }

        private bool IsInline {
            get {
                return _parser.IsInline;
            }
        }

        private bool HandleExpressionBlock(char chr, int index) {

            ProcessBlockStartEndLiterals(chr);

            var nextChar = _parser.NextChar();
            string currentBuffer = null;
            char nextNextChar = RazorParser.Null;
            var isNextEnd = (_parser.IsTerminateChar(nextChar) ||
                (nextChar == RazorParser.SingleQuoteChr && _parser.IsTerminateChar((nextNextChar = nextNextChar == RazorParser.Null ? _parser.GetCharacterAtIndex(index + 2) : nextNextChar))) ||
                (nextChar == RazorParser.QuoteChr && _parser.IsTerminateChar((nextNextChar = nextNextChar == RazorParser.Null ? _parser.GetCharacterAtIndex(index + 2) : nextNextChar))) ||
                (nextChar == RazorParser.QuoteChr && _charBeforeRazor == nextChar && (( (currentBuffer = currentBuffer ?? _buffer.ToString()).Count(x => x == nextChar) + 2) % 2 == 0)) ||
                (nextChar == RazorParser.SingleQuoteChr && _charBeforeRazor == nextChar && (((currentBuffer = currentBuffer ?? _buffer.ToString()).Count(x => x == nextChar) + 2) % 2 == 0))) &&
                (_bbcount == null || _bbcount == 0);

            if (RazorParser.ValidEndCharacters.Contains(chr)) {
                var detectEmail = !isNextEnd && (chr == RazorParser.DotChr && nextChar != RazorParser.DotChr) && !_parser.IsTerminateChar(_charBeforeRazor) 
                    && _charBeforeRazor != RazorParser.RazorChr && _charBeforeRazor != RazorParser.TagEndChr;
                
                if (detectEmail) {
                    _parser.WriteHtml(_parser.GetString(_razorIndex, index));
                    _buffer.Clear();
                    return true;
                } else {
                    if (isNextEnd) {
                        EndExpressionBlock(() => _parser.WriteHtml(chr));
                        return true;
                    } else {
                        _buffer.Append(chr);
                    }
                }
            } else {
                _buffer.Append(chr);
            }

            if (isNextEnd) {
                EndExpressionBlock();
                return true;
            }
            return false;
        }

        private void EndExpressionBlock(Action completeAction = null) {
            if (IsInline) {
                var id = _parser.GenerateHtmlReplaceCode(_buffer.ToString());
                _parser.WriteHtml(String.Format("<span data-id='{0}'></span>", id));

            } else {
                _parser.WriteCode(_buffer.ToString());
            }
            if (completeAction != null) completeAction();
            _buffer.Clear();
        }

        private bool HandleExplicitExpression(char chr, int index) {
            ProcessStartEndLiterals(chr);
            if (_ecounter == 0) {
                EndExpressionBlock();
                return true;
            } else {
                _buffer.Append(chr);
            }
            return false;
        }

        private bool HandleIfExpression(char chr, int index) {
            ProcessStartEndLiterals(chr);

            if (_bcounter == 0) {
                _parser.WriteCode(chr);
                int nextIndex = index + 1,
                    operandElseIndex = _parser.FindNextChars(nextIndex, "else{"),
                    operandElseIfIndex = _parser.FindNextChars(nextIndex, "else if(");

                if (operandElseIfIndex > -1) {
                    _parser.charIndex = operandElseIfIndex;
                    _parser.WritePlainCode("else if(");
                    MoveToBlockStart();
                } else if (operandElseIndex > -1) {
                    _parser.charIndex = operandElseIndex;
                    _parser.WritePlainCode("else{\r\n");
                    _bcounter = 1;
                } else {
                    _parser.ReplaceHtmlWriteAndRender();
                    if (_root == null)
                        _parser.EndViewResponse();
                    _parser.charIndex++;
                    return true;
                }
            } else {
                HandleCode(chr);
            }

            return false;
        }

        private bool HandleDoWhileExpression(char chr, int index) {
            ProcessStartEndLiterals(chr);

            if (_bcounter == 0) {

                _parser.WriteCode(chr);

                var count = 1;
                char current = RazorParser.Null;

                while (true) {
                    current = _parser.GetCharacterAtIndex(++_parser.charIndex);
                    _parser.WriteCode(current);
                    if (current == RazorParser.ExplicitBlockStart) break;
                }

                while(true) {
                    current = _parser.GetCharacterAtIndex(++_parser.charIndex);
                    _parser.WriteCode(current);
                    if (current == RazorParser.ExplicitBlockStart) count++;
                    else if (current == RazorParser.ExplicitBlockEnd) count--;
                    if (count == 0) break;
                }
                _parser.ReplaceHtmlWriteAndRender();
                if (_root == null)
                    _parser.EndViewResponse();
                _parser.charIndex++;
                return true;
            } else {
                HandleCode(chr);
            }

            return false;
        }

        private bool HandleComment(char chr, int index) {

            if (chr == RazorParser.CommentChar && _parser.NextChar() == RazorParser.RazorChr) {
                _parser.charIndex += 2;
                if (_root != null)
                    _parser.WritePlainCode("*/");
                else
                    _parser.WritePlainHtml("-->");
                return true;
            } else {
                if (_root != null)
                    _parser.WriteCode(chr);
                else
                    _parser.WriteHtml(chr);
            }

            return false;
        }

        private void WriteBuffer(char chr) {
            if (chr == RazorParser.ReturnChr || chr == RazorParser.NewLineChr) return;
            _buffer.Append(chr);
        }

        private bool HandleText(char chr, int index) {
            if (chr == RazorParser.NewLineChr || chr == RazorParser.ReturnChr) {
                WriteBuffer(chr);
                FlushBuffer();
                if (_root == null)
                    _parser.EndViewResponse();
                return true;
            }
            else if (chr == RazorParser.RazorChr) {
                FlushBuffer();
                IDocumentParser parser = new RazorDocumentParser(_root != null ? _root : this);
                parser.Execute(_parser);
            } else {
                WriteBuffer(chr);
            }
            return false;
        }

        private bool HandleCodeBlock(char chr, int index) {
            ProcessStartEndLiterals(chr);

            if (_bcounter == 0) {
                _parser.ReplaceHtmlWriteAndRender();
                if (_root == null)
                    _parser.EndViewResponse();
                _parser.charIndex++;
                return true;
            } else {
                HandleCode(chr);
            }

            return false;
        }

        private bool HandleHelperBlock(char chr, int index) {
            ProcessStartEndLiterals(chr);

            if (_bcounter == 0) {
                _parser.ReplaceHtmlWriteAndRender();
                _parser.WritePlainCode("return {0}.GetBuffer();\r\n", _parser.viewResponseID);
                _parser.WriteCode(chr);
                _parser.EndViewResponse();
                _parser.helper = false;
                _parser.charIndex++;
                return true;
            } else {
                HandleCode(chr);
            }

            return false;
        }

        private void FlushBuffer() {
            if (_buffer.Length > 0) {
                //Flush current html
                _parser.WriteHtml(_buffer.ToString());
                _buffer.Clear();
            }
        }

        private void Parse() {
            if (_blockType == BlockType.Code)
                _parser.ProcessBlock(HandleCodeBlock);
            else if (_blockType == BlockType.Text) {
                _parser.StartViewResponse();
                _parser.ProcessBlock(HandleText);
            } else if (_blockType == BlockType.ExplicitExpression) {
                _parser.ProcessBlock(HandleExplicitExpression);
            } else if (_blockType == BlockType.Expression) {
                _parser.ProcessBlock(HandleExpressionBlock);
            } else if (_blockType == BlockType.If) {
                MoveToBlockStart();
                _parser.ProcessBlock(HandleIfExpression);
            } else if (_blockType == BlockType.Helper) {
                MoveToBlockStart();
                _parser.StartViewResponseForHelper();
                _parser.ProcessBlock(HandleHelperBlock);
            } 
            else if (_blockType == BlockType.For || _blockType == BlockType.Switch || _blockType == BlockType.While || _blockType == BlockType.With) {
                MoveToBlockStart();
                _parser.ProcessBlock(HandleCodeBlock);
                _parser.WriteCode(RazorParser.BlockEndBrace);
            } else if (_blockType == BlockType.Comment) {
                _parser.ProcessBlock(HandleComment);
            } else if (_blockType == BlockType.DoWhile) {
                _parser.ProcessBlock(HandleDoWhileExpression);
            }
        }

        private void HandleCode(char chr) {
            if (chr == RazorParser.TagStartChr) {
                var prev = _parser.GetCharacterAtIndex(_parser.charIndex - 1);
                var isQuote = prev == RazorParser.SingleQuoteChr || prev == RazorParser.QuoteChr;
                if (isQuote) {
                    while (true) {
                        chr = _parser.GetCharacterAtIndex(_parser.charIndex);
                        _parser.WriteCode(chr);
                        if (chr == prev && _parser.GetCharacterAtIndex(_parser.charIndex - 1) != RazorParser.EscapeChr) break;
                        _parser.charIndex++;
                    }
                    return;
                }
                ExecuteDocumentParser();
            } else if (chr == RazorParser.RazorChr) {
               ExecuteRazorDocumentParser();
            } else {
                _parser.WriteCode(chr);
            }
        }

        private void ExecuteDocumentParser() {
            _parser.StartViewResponse();
            IDocumentParser parser = new HtmlDocumentParser(_htmlParser, this);
            parser.Execute(_parser);
        }

        private void ExecuteRazorDocumentParser() {
            if (_parser.NextChar() != RazorParser.CommentChar)
                _parser.StartViewResponse();
            IDocumentParser parser = new RazorDocumentParser(_root != null ? _root : this, _htmlParser);
            parser.Execute(_parser);
        }

        private void MoveToBlockStart() {
            var current = _parser.GetCharacterAtIndex(_parser.charIndex);
            while (current != RazorParser.BlockStartBrace) {
                _parser.WriteCode(current);
                current = _parser.GetCharacterAtIndex(++_parser.charIndex);
            }
            _bcounter = 1;
            _parser.WriteCode(current);
            _parser.charIndex++;
        }

        private bool IsInHtml {
            get {
                return _htmlParser != null;
            }
        }

        private void ProcessBlockStartEndLiterals(char chr) {
            if (chr == RazorParser.ExplicitBlockStart || chr == RazorParser.BlockStartBrace || chr == RazorParser.BracketStartChr) {
                //if (!IsInHtml && IsCharInQuoteOrComment(chr)) return;
                if (_bbcount == null) {
                    _bbcount = 0;
                }
                if (_bbcount.HasValue)
                    _bbcount += 1;
            } else if (chr == RazorParser.ExplicitBlockEnd || chr == RazorParser.BlockEndBrace || chr == RazorParser.BracketEndChr) {
                //if (!IsInHtml && IsCharInQuoteOrComment(chr)) return;
                if (!_bbcount.HasValue)
                    throw new InvalidOperationException(String.Format("Invalid character '{0}' for {1}", chr, _buffer));
                _bbcount -= 1;
            }
        }

        private bool IsCharInQuoteOrComment(char chr) {
            var index = _parser.charIndex;
            var current = _parser.GetCharacterAtIndex(index);
            var nextCharIsEnd = _parser.IsTerminateChar(_parser.NextChar());

            while (current != RazorParser.RazorChr) {
                index--;
                current = _parser.GetCharacterAtIndex(index);
                if ((current == RazorParser.QuoteChr || current == RazorParser.SingleQuoteChr) && RazorParser.StartBlocks.Contains(_parser.GetCharacterAtIndex(index - 1))) return true && !nextCharIsEnd;
                if ((current == RazorParser.CommentChar && _parser.GetCharacterAtIndex(index - 1) == RazorParser.TagCloseChr)) return true && !nextCharIsEnd;
            }

            return false;
        }

        private void ProcessStartEndLiterals(char chr) {
            var isCodeBlock = (((BlockType)((int)_blockType >> 2)) & BlockType.Code) == BlockType.Code;
            if (chr == RazorParser.ExplicitBlockStart || chr == RazorParser.BlockStartBrace) {
                if (_blockType == BlockType.Code || isCodeBlock)
                    _bcounter++;
                else
                    _ecounter++;
            } else if (chr == RazorParser.ExplicitBlockEnd || chr == RazorParser.BlockEndBrace) {
                if (_blockType == BlockType.Code || isCodeBlock) {
                    if(_bcounter < 0)
                        throw new InvalidOperationException(String.Format("Invalid character '{0}' for code block", chr));
                    _bcounter--;
                } else {
                    if (_ecounter < 0)
                        throw new InvalidOperationException(String.Format("Invalid character '{0}' for explicit expression", chr));
                    _ecounter--;
                }
            }
        }
    }
}
