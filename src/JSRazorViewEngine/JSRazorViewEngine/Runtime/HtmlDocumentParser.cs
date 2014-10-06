using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JSRazorViewEngine.Interfaces;
using System.Text.RegularExpressions;

namespace JSRazorViewEngine.Runtime {
    internal unsafe sealed class HtmlDocumentParser : IDocumentParser {

        private StringBuilder _buffer;
        private RazorParser _parser;
        private HtmlDocumentParser _root;
        private RazorDocumentParser _razor;
        private bool _textMode;
        private int _tcounter;
        private static Regex _expressionRegex = new Regex(@"<([^<@'""]+)(/s+)?(""|')?@+([^@]+)(\s*)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private bool IsInline {
            get {
                return _parser.IsInline;
            }
        }

        public HtmlDocumentParser() {
            _buffer = new StringBuilder();
        }

        public HtmlDocumentParser(HtmlDocumentParser root) : this() {
            _root = root;
        }

        public HtmlDocumentParser(HtmlDocumentParser root, RazorDocumentParser razor) : this() {
            _root = root;
            _razor = razor;
        }

        void IDocumentParser.Reset() {
            _buffer = new StringBuilder();
        }

        private void WriteBuffer(char chr) {
            if (chr == RazorParser.ReturnChr || chr == RazorParser.NewLineChr) return;
            _buffer.Append(chr);
        }

        private bool Process(char chr, int index) {
            if (chr == RazorParser.RazorChr) {
                if (_buffer.Length > 0) {
                    _parser.WriteHtml(_buffer.ToString());
                    _buffer.Clear();
                }
                IDocumentParser parser = new RazorDocumentParser(_razor, _root != null ? _root : this);
                parser.Execute(_parser);
            } else {
                WriteBuffer(chr);
                char prev = _parser.GetCharacterAtIndex(index - 1), next = _parser.GetCharacterAtIndex(index + 1);

                if (_textMode && _buffer.Length >= 6 && chr == RazorParser.TagEndChr) {
                    if (_parser.FindNextChars(index - 5, RazorParser.TextTag) > -1) {
                        _buffer.Remove(_buffer.Length - 6, 6);
                    }
                } else if (chr == RazorParser.TagStartChr && (prev != RazorParser.QuoteChr || prev != RazorParser.SingleQuoteChr) && (next != RazorParser.TagCloseChr)) {
                    if (_textMode) {
                        if (_parser.FindNextChars(index, RazorParser.TextTag) > -1)
                            _tcounter++;
                    } else
                        _tcounter++;
                } else if (chr == RazorParser.TagCloseChr && (prev == RazorParser.TagStartChr || next == RazorParser.TagEndChr)) {
                    if (_textMode) {
                        if (_parser.FindNextChars(index - 1, RazorParser.TextCloseTag) > -1)
                            _tcounter--;
                    } else
                        _tcounter--;
                }

                if (_tcounter == 0) {
                    while (true) {
                        chr = _parser.GetCharacterAtIndex(++_parser.charIndex);
                        if (!_textMode)
                            WriteBuffer(chr);
                        if (chr == RazorParser.TagEndChr) break;
                    }
                    if (_textMode)
                        _buffer.Remove(_buffer.Length - 2, 2);
                    _parser.WriteHtml(_buffer.ToString());
                    if (_razor == null)
                        _parser.EndViewResponse();
                    _buffer.Clear();
                    return true;
                }
            }
            return false;
        }

        private void RemoveTextTag(bool removeOpenTag = true) {
            if (_buffer.Length < 6) return;
            var html = _buffer.ToString();
            if (removeOpenTag) {
                if (html.Contains(RazorParser.TextTag))
                    _buffer.Replace(RazorParser.TextTag, string.Empty);
                if (html.Contains(RazorParser.TextCloseTag))
                    _buffer.Replace(RazorParser.TextCloseTag, string.Empty);
            } else if (!removeOpenTag) {
                if(html.Contains(RazorParser.TextCloseTag))
                    _buffer.Replace(RazorParser.TextCloseTag, string.Empty);
            }
        }

        void IDocumentParser.Execute(RazorParser parser) {
            _parser = parser;
            var prevIndex = _parser.charIndex - 1;
            var prevChar = _parser.GetCharacterAtIndex(prevIndex);
            if (prevChar == RazorParser.QuoteChr || prevChar == RazorParser.SingleQuoteChr) {
                _parser.WriteHtml(RazorParser.TagStartChr);
                return;
            }
            var textIndex = _parser.FindNextChars(_parser.charIndex, RazorParser.TextTag);
            _tcounter = 1;
            if (textIndex > -1) {
                _parser.charIndex = textIndex;
                _textMode = true;
                _parser.StartViewResponse();
            } else {
                _parser.charIndex++;
                if (NeedViewResponse()) _parser.StartViewResponse();
                _buffer.Append(RazorParser.TagStartChr);
            }
            _parser.ProcessBlock(Process);
        }

        private bool NeedViewResponse() {
            int counter = 1, index = _parser.charIndex, openCounter = 1;
            var sb = new StringBuilder();
            char current = RazorParser.Null, prev = RazorParser.Null;

            while (true) {
                current = _parser.GetCharacterAtIndex(++index);
                if (current == RazorParser.Null) break;
                if (current == RazorParser.TagStartChr && (prev != RazorParser.QuoteChr || prev != RazorParser.SingleQuoteChr)) counter++;
                else if (current == RazorParser.TagCloseChr && (prev == RazorParser.TagStartChr || _parser.template[index + 1] == RazorParser.TagEndChr)) counter--;
                if (current == RazorParser.TagStartChr && _parser.GetCharacterAtIndex(index + 1) != RazorParser.TagCloseChr) openCounter++;
                else if (current == RazorParser.TagEndChr) openCounter--;
                if (openCounter > 0 && current == RazorParser.RazorChr) return true;
                if (_root != null && current == RazorParser.RazorChr && _parser.GetCharacterAtIndex(index + 1) == RazorParser.TextChr) return true;
                if (counter == 0) break;
                prev = current;
            }
            return false;
        }
    }
}
