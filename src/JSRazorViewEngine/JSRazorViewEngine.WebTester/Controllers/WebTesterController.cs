using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace JSRazorViewEngine.WebTester.Controllers
{
    public class WebTesterController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        [ValidateInput(false)]
        public string GetRazorResult(string model, string razorTemplate) {
            var result = RazorJs.Parse(razorTemplate, model: model.TrimEnd('\n','\r'));
            return result;
        }
    }
}
