using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JSRazorViewEngine.Runtime;

namespace JSRazorViewEngine {
    public sealed class RazorJs {

        public static string Parse(string viewName, string template, object model = null) {
            return RazorParser.Compile(viewName, template, model);
        }

        public static string Parse(string template, object model = null) {
            var viewName = Guid.NewGuid().ToString().Replace("-", string.Empty);
            var result = Parse(viewName, template, model);
            return result;
        }
    }
}
