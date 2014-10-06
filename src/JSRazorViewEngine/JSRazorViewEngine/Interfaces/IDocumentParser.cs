using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JSRazorViewEngine.Runtime;

namespace JSRazorViewEngine.Interfaces {
    internal interface IDocumentParser {
        void Reset();
        void Execute(RazorParser parser);
    }
}
