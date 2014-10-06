using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSRazorViewEngine.Runtime {
    internal unsafe static class RazorParserExtensions {

        internal static bool IsTerminateChar(this RazorParser parser, char chr) {
            return chr == RazorParser.SpaceChr || chr == RazorParser.Null || chr == RazorParser.NewLineChr || chr == RazorParser.ReturnChr || chr == RazorParser.TagStartChr ||
                chr == RazorParser.TabChr || chr == RazorParser.SemiColonChr;
        }

        internal static char GetCharacterAtIndex(this RazorParser parser, int index) {
            var chr = RazorParser.Null;
            try {
                fixed (char* p = parser.template) {
                    chr = *(p + index);
                }
            } catch { }
            return chr;
        }

        internal static string GetString(this RazorParser parser, int startIndex, int endIndex) {
            var size = endIndex - startIndex + 1;
            var str = new string(RazorParser.Null, size);
            fixed (char* p = parser.template) {
                fixed (char* cp = str)
                    RazorHelper.memcpy(cp, p + startIndex, size);
            }
            return str;
        }


        internal static int FindNextChars(this RazorParser parser, int startIndex, string chars) {
            return parser.FindNextChars(startIndex, chars.ToArray());
        }

        internal static int FindNextChars(this RazorParser parser, int startIndex, params char[] chars) {
            var index = 0;
            var current = RazorParser.Null;
            var cbuffer = new char[chars.Length];

            fixed (char* p = parser.template) {
                while (startIndex < parser.templateLength) {
                    if (index == chars.Length) break;
                    current = *(p + startIndex);
                    startIndex++;
                    if (parser.IsTerminateChar(current) && !chars.Contains(current)) continue;
                    cbuffer[index] = current;
                    index++;
                }
            }

            return RazorHelper.IsEqual(chars, cbuffer) ? startIndex : -1;
        }

        internal static bool HasNextChars(this RazorParser parser, int index, char[] chars) {
            fixed (char* p = parser.template) {
                char* chr = p + index;
                char current = *chr;
                for (var i = 0; i < chars.Length; i++) {
                    if (chars[i] == current)
                        return true;
                }
            }
            return false;
        }

        internal static char NextChar(this RazorParser parser) {
            return parser.charIndex < parser.templateLength -1 ? parser.template[parser.charIndex + 1] : RazorParser.Null;
        }
    }
}
