using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections.Concurrent;
using JSRazorViewEngine.Interfaces;

namespace JSRazorViewEngine.Runtime {

    internal unsafe class RazorParser {

        private ConcurrentDictionary<string, string> _ids = new ConcurrentDictionary<string, string>();
        private HashSet<string> _scripts = new HashSet<string>();

        internal const char RazorChr = '@', Null = '\0', CommentChar = '*', BlockStartBrace = '{', EqualChr = ']',
            BlockEndBrace = '}', ExplicitBlockStart = '(', ExplicitBlockEnd = ')', SpaceChr = ' ', DotChr = '.',
            TagStartChr = '<', QuoteChr = '"', SingleQuoteChr = '\'', BracketStartChr = '[', BracketEndChr = ']',
            TagEndChr = '>', TagCloseChr = '/', TextChr = ':', NewLineChr = '\n', ReturnChr = '\r', TabChr = '\t', 
            SemiColonChr = ';', EscapeChr = '\\';

        internal string template, viewResponseID;

        internal static char[] StartBlocks = new[] { RazorParser.ExplicitBlockStart, RazorParser.BracketStartChr };

        private string _viewName;

        private static long _id = 0;
        private static readonly object _lockObject = new object();
        private static readonly string _idprefix = "v";

        internal static readonly string TextTag = "<text>",
            TextCloseTag = "</text>", ValidEndCharacters = "!.?/~$%^&*-+#`";

        private StringBuilder _buffer, _blockBuffer, _scriptBuffer, _jQueryScript, _viewResponseBuffer, _helperBuffer;
        private object _model;
        internal int templateLength;

        internal int charIndex;
        internal bool helper;

        private static JavaScriptSerializer _jsSerializer = new JavaScriptSerializer();

        private RazorParser(string viewName, string template, object model) {
            this.template = template;
            _viewName = viewName;
            charIndex = 0;
            _viewResponseBuffer = new StringBuilder();
            _blockBuffer = new StringBuilder();
            _scriptBuffer = new StringBuilder();
            _jQueryScript = new StringBuilder();
            _buffer = new StringBuilder();
            _helperBuffer = new StringBuilder();
            templateLength = template.Length;
            _model = model;
        }

        internal void ReplaceHtmlWriteAndRender() {
            var scriptBuffer = helper ? _helperBuffer : _scriptBuffer;
            var code = scriptBuffer.ToString();
            var hasRenderOrWrite = code.Contains("Html.Write(") || code.Contains(".Render(this)");
            if (!hasRenderOrWrite) return;
            StartViewResponse();
            scriptBuffer.Replace(".Render(this)", String.Format(".Render({0})", viewResponseID));
            scriptBuffer.Replace("Html.Write(", String.Format("{0}.Write(", viewResponseID));
        }

        internal bool IsInline {
            get {
                return String.IsNullOrWhiteSpace(viewResponseID); ;
            }
        }

        internal string NextID() {
            lock (_lockObject) {
                _id++;
                return String.Concat("_", _id);
            }
        }

        internal void WriteHtml(string html, params object[] args) {
            if (html.Length == 0) return;
            if (!IsInline) {
                var scriptBuffer = helper ? _helperBuffer : _scriptBuffer;
                scriptBuffer.AppendFormat("{0}.Write(\"{1}\");\r\n", viewResponseID, html.Replace("\"", @"\"""));
                return;
            }
            _buffer.AppendFormat(html, args);
        }

        internal void WritePlainHtml(string html, params object[] args) {
            _buffer.AppendFormat(html, args);
        }

        internal void WriteHtml(char chr) {
            var value = chr.ToString();
            WriteHtml(chr == RazorParser.BlockStartBrace || chr == RazorParser.BlockEndBrace ? String.Concat(chr, chr) : value);
        }

        internal void WriteHtmlReplaceCode(string code, params object[] args) {
            _jQueryScript.AppendFormat(code, args);
        }

        internal string GenerateHtmlReplaceCode(string code) {
            var id = _ids.GetOrAdd(code, _ => NextID());
            if (!_scripts.Contains(id)) {
                var hasRenderOrWrite = code.Contains("Html.Write(") || code.Contains(".Render(this)");
                
                if (!hasRenderOrWrite) {
                    _scriptBuffer.AppendFormat("function {0}(){{ var result = {1}{2} return typeof(result) == 'undefined' ? ' ' : result; }}\r\n", id, code, code.EndsWith(";") ? string.Empty : ";");
                    _jQueryScript.AppendFormat("jQuery(\"span[data-id='{0}']\").replaceWith({0}());\r\n", id);
                } else {
                    var viewID = String.Concat(_idprefix, id);
                    code = code.Replace(".Render(this)", String.Format(".Render({0})", viewID));
                    code = code.Replace("Html.Write(", String.Format("{0}.Write(", viewID));
                    _scriptBuffer.AppendFormat("function {0}(){{ var {1} = new ViewResponse(); {2}{3} return {1}.GetBuffer(); }}\r\n", id,
                        viewID, code, code.EndsWith(";") ? string.Empty : ";");
                    _jQueryScript.AppendFormat("jQuery(\"span[data-id='{0}']\").replaceWith({0}());\r\n", id);
                }
                _scripts.Add(id);
            }
            return id;
        }

        internal void WriteCode(string code) {
            var scriptBuffer = helper ? _helperBuffer : _scriptBuffer;
            if (!IsInline) {
                scriptBuffer.AppendFormat("{0}.Write(({1}));\r\n", viewResponseID, code);
                return;
            }
            scriptBuffer.Append(code); 
        }

        internal void WriteCode(char chr) {
            var scriptBuffer = helper ? _helperBuffer : _scriptBuffer;
            scriptBuffer.Append(chr);
        }

        internal void WritePlainCode(string code, params object[] args) {
            var scriptBuffer = helper ? _helperBuffer : _scriptBuffer;
            var needReplace = (code.Count(x => x == BlockEndBrace) + code.Count(x => x == BlockStartBrace)) % 2 != 0;
            if (needReplace) {
                code = code.Replace(RazorParser.BlockStartBrace.ToString(), String.Concat(RazorParser.BlockStartBrace, RazorParser.BlockStartBrace));
                code = code.Replace(RazorParser.BlockEndBrace.ToString(), String.Concat(RazorParser.BlockEndBrace, RazorParser.BlockEndBrace));
            }
            scriptBuffer.AppendFormat(code, args);
        }

        internal void ProcessBlock(Func<char, int, bool> func) {
            char current = Null;
            fixed (char* p = template) {
                while (charIndex < templateLength) {
                    current = *(p + charIndex);
                    if (current == Null) break;
                    if (func != null) 
                        if(func(current, charIndex)) 
                            break;
                    ++charIndex;
                }
            }
        }

        private void ProcessBlock(Action<char, char, int> action) {
            char current = Null, prev = Null;
            fixed (char* p = template) {
                while (charIndex < templateLength) {
                    current = *(p + charIndex);
                    if (current == Null) break;
                    if (action != null) action(current, prev, charIndex);
                    ++charIndex;
                    prev = current;
                }
            }
        }

        internal void StartViewResponse() {
            if (!String.IsNullOrWhiteSpace(viewResponseID)) return;
            viewResponseID = NextID();
            _viewResponseBuffer.AppendFormat("var {0} = new ViewResponse();\r\n", viewResponseID);
            _buffer.AppendFormat("<span data-id='{0}'></span>\r\n", viewResponseID);
        }

        internal void StartViewResponseForHelper() {
            viewResponseID = NextID();
            _helperBuffer.AppendFormat("var {0} = new ViewResponse();\r\n", viewResponseID);
        }

        internal void EndViewResponse() {
            if (String.IsNullOrWhiteSpace(viewResponseID)) return;
            if (!helper)
                WriteHtmlReplaceCode("jQuery(\"span[data-id='{0}']\").replaceWith({0}.GetBuffer());\r\n", viewResponseID); ;
            viewResponseID = null;
        }

        private void ReadTemplate(char chr, char prev, int index) {
            //TODO avoid multiple instances
            if (chr == TagStartChr) {
                IDocumentParser parser = new HtmlDocumentParser();
                parser.Execute(this);
            } else if (chr == RazorChr) {
                IDocumentParser parser = new RazorDocumentParser();
                parser.Execute(this);
            } else {
                _buffer.Append(chr);
            }
        }

        private string Parse() {
            ProcessBlock(ReadTemplate);
            if (viewResponseID != null)
                EndViewResponse();
            var result = String.Format(@"
<script type='text/javascript'>
    var Model = {1}
    window['{2}'] = (function(){{
      {3}
      {4}
      {5}
      this._hackToInvokeReplacementCorrectly = function (ele){{ 
        {6}
        jQuery(ele).remove();
      }}
      return this;
    }})();
</script>
{0}
<img src=""data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"" onload=""window['{2}']._hackToInvokeReplacementCorrectly(this);"" />
", _buffer, _model == null ? "{}" : (_model.GetType() == typeof(string) ? _model.ToString() : _jsSerializer.Serialize(_model)), _viewName,
 _helperBuffer,
 _viewResponseBuffer,
_scriptBuffer, _jQueryScript);
            _scriptBuffer.Clear();
            _jQueryScript.Clear();
            _buffer.Clear();
            _ids.Clear();
            _scripts.Clear();
            return result;
        }

        internal static string Compile(string viewName, string template, object model) {
            var parser = new RazorParser(viewName, template, model);
            return parser.Parse();
        }
    }
}
