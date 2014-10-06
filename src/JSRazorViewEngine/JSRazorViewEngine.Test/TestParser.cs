using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using JSRazorViewEngine.Runtime;

namespace JSRazorViewEngine.Test {
	/// <summary>
	/// Summary description for UnitTest1
	/// </summary>
	[TestClass]
	public class TestParser {
		public TestParser() {
			//
			// TODO: Add constructor logic here
			//
		}

		private TestContext testContextInstance;

		/// <summary>
		///Gets or sets the test context which provides
		///information about and functionality for the current test run.
		///</summary>
		public TestContext TestContext {
			get {
				return testContextInstance;
			}
			set {
				testContextInstance = value;
			}
		}

		#region Additional test attributes
		//
		// You can use the following additional attributes as you write your tests:
		//
		// Use ClassInitialize to run code before running the first test in the class
		// [ClassInitialize()]
		// public static void MyClassInitialize(TestContext testContext) { }
		//
		// Use ClassCleanup to run code after all tests in a class have run
		// [ClassCleanup()]
		// public static void MyClassCleanup() { }
		//
		// Use TestInitialize to run code before running each test 
		// [TestInitialize()]
		// public void MyTestInitialize() { }
		//
		// Use TestCleanup to run code after each test has run
		// [TestCleanup()]
		// public void MyTestCleanup() { }
		//
		#endregion

		private static void TestTemplate(string template) {
			var result = RazorJs.Parse(template, new { ID = "Tester" });

			Assert.IsTrue(!result.Contains("@"));
		}

        [TestMethod]
        public void CanParseTableWithRazorExpression() {
            var template = @"
<table cellpadding=""0"" cellspacing=""0"" border=""0"" style=""height: 84px; box-sizing: border-box; -moz-box-sizing: border-box;"">
    <tr>    
        <td style=""padding: 10px;"" rowspan=""2"">
            <div class=""icon-globe"" style=""font-size: 64px; text-shadow: 1px 4px 6px #def, 0 0 0 #000, 1px 4px 6px #def; color: rgba(10,60,150, 0.8);""></div>
        </td>
        <td style=""vertical-align: top; padding: 10px;"">
            <h1>Offline</h1>
            Server is currently offline.
            <span data-retry-message>
                <span data-retry-failed>
                    Next attempt in 30 seconds..
                </span>
            </span>
        </td>
    </tr>
    <tr>
        <td style=""vertical-align: top; padding: 10px;"">
            <span data-retry-manual style=""float: right;"">
                @Button.Create(function(s) {
                    s.SetText('Re-Connect');
                }).Render(this)
            </span>
        </td>
    </tr>
</table>";

            TestTemplate(template);
        }


        [TestMethod]
        public void CanParseKyleRazorStatementFailingWithInputFormat() {
            var template = @"
<span class=""popupText"" data-text></span><br />
<div style=""float:right"">
    <span data-deny>
    @Button.Create(function (s) {
        s.SetText('Decline');
        s.RemoveClass('uixPalette_0');
        s.AddClass('uixPalette_1');
    }).Render(this)
    </span>
    <span data-accept>
    @Button.Create(function (s) {
        s.SetText('Accept');
    }).Render(this)
    </span>
</div>
<div style=""clear:both;""></div>";

            TestTemplate(template);
        }

		[TestMethod]
		public void CanParseExpressionWithTextStatement() {
			var template = @"
@{
	var none = 'Blanky';
	var products = [{Name: 'Test', ID: 1, InStock: true}, {Name: 'Test2', ID: 2, InStock: false}, {Name: 'Test3', ID: 3, InStock: false}, {Name: 'Test4', ID: 4, InStock: true}];
}

<h2>Products</h2>

<ul>
@for (var p in products){
	var product = products[p];
	<li>
		@product.Name
		
		@if(!product.InStock){
			@: (Out of Stock!)
			@: @none (Does not exist)
		}
	</li>
}
</ul>
@: Model ID = @Model.ID
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithMethodCallContainHtmlAndExpression() {
			var template = @"
@{
	var values = [1,2,3,4,5,6,7];
}

@{
	values.forEach(function(item){
		<span>@item</span><br/>
	})
}

@: Model ID = @Model.ID
";
			TestTemplate(template);
		}

		[TestMethod]
		public void CannotParseInvalidRazorSyntax() {
			var template = @"
@ Model.ID
@{
  var test = 'Test';
}
";
			Exception ex = null;
			try {
				RazorJs.Parse(template, new { ID = "Tester" });
			} catch (Exception e) {
				ex = e;
			}
			Assert.IsNotNull(ex);
		}

		[TestMethod]
		public void CanParseExpressionWithDoWhileStatement() {
			var template = @"
@{
	var index = 0;
	var maxIndex = 10;
}

@do {
	index++;
	<span>@index</span><br/>
} while(index < maxIndex);
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithWhileStatement() {
			var template = @"
@{
	var index = 0;
	var maxIndex = 10;
}

@while (index < maxIndex){
	index++;
	<span>@index</span><br/>
}
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionCommentAndIgnoreParsingofRazorInComment() {
			var template = @"
@*
	@if(true){
		<span>@test</span>
	}
*@
";
			var result = RazorJs.Parse(template, new { ID = "Tester" });

			Assert.IsTrue(result.Contains("@if"));
		}


		[TestMethod]
		public void CanParseExpressionCommentToHtmlComment() {
			var template = @"
@*
	This is a multiline comment for span
	This is a multiline comment for span
*@
<span>Hello world</span>
";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionCommentInCodeBlock() {
			var template = @"
@{
	@*
		This is a multiline comment
		This is a multiline comment
	*@
}
";
			TestTemplate(template);
		}


		[TestMethod]
		public void CanParseExpressionWithForStatement() {
			var template = @"
@{
	var products = [{Name: 'Test', ID: 1}, {Name: 'Test2', ID: 2}, {Name: 'Test3', ID: 3}];
}

<h2>Products</h2>

<ul>
@for (var p in products){
	<li>@products[p].Name</li>
}
</ul>
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithSwitchStatement() {
			var template = @"
@{
	var value = 'World';
	var man = 'Man';
	var woman = 'Woman';
}

@switch(value){
	case 'World':
		<span>It is a @man world</span>
		break;
	default:
		<span>It is a @woman world</span>
}
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithIfStatement() {
			var template = @"
@{
	var value = 'Planet';
	var man = 'Man';
	var woman = 'Woman';
}
@if(value == 'World'){
	<span>It is a @man world</span>
}
else if(value == 'Planet'){
	<span>It is just our planet</span>
}
else{
	<span>It is a @woman world</span>
}
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithDoubleRazorChar() {
			var template = @"
@{
	var value = 'World';
}
<span>@@value</span>
			";

			var result = RazorJs.Parse(template);

			Assert.IsTrue(result.Contains("@value"));
		}

		[TestMethod]
		public void CanParseExpressionWithEmail() {
			var template = @"
@{
	var value = 'World';
}
<span>Hello@value.com</span>
			";

			var result = RazorJs.Parse(template);

			Assert.IsTrue(result.Contains("Hello@value.com"));
		}

		[TestMethod]
		public void CanParseExpressionWithVariousEndingCharacter() {
			var template = @"
@{
	var value = 'World';
}
<span>@value. @value? @value$ @value! @value/ @value% @value^ @value& @value# @value* @value- @value+ @value~ @value</span>
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithMethodCallContainingStringWithBraces() {
			var template = @"
@{
	var value = 'World';
	var func = function(str){
		return str;
	}
}
<span>@func('(test)')</span>
<span>@func(')test(')</span>
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithStartingText() {
			var template = @"
@{
	var value = 'World';
}
<span>Hello@value</span>
<span>@Model.ID</span>
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionInAttributeWithExplicitParam() {
			var template = @"
@{
	var attributeValue = 'Test';
	var value = 'Value';
	var anotherAttr = 'Something';
}
<span special = '@(anotherAttr) blah @value @value. blah' class='test' id='@(attributeValue)'>@(value)<p>blah</p></span>
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionInAttributeWithMethodCallUsingSingleQuote() {
			var template = @"
@{
	var attributeValue = 'Test';
	var value = 'Value' + new Date().getTime();
	var anotherAttr = 'Something';
	var func = function(str){
		return str;
	}
}
<span special = '@func('FunctionText') @anotherAttr' class='test' id='@attributeValue'>@value</span>
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionInAttribute() {
			var template = @"
@{
	var attributeValue = 'Test';
	var value = 'Value';
	var anotherAttr = 'Something';
}
<span special = '@anotherAttr' class='test' id='@attributeValue'>@value</span>
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithDot() {
			var template = @"
@{
	var bar = 'Bar';
	var foo = 'Foo';
}
<span>@foo. @bar.</span>
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionUnbalancedTagWithTextElement() {
			var template = @"
@{
	var value = 'Hello World';
	var value2 = 'Universe';
}
<text>
	<p><a><img src='@(value2).png'>
	<p><span>
</text>
";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithTextElement() {
			var template = @"
@{
	var value = 'Hello World';
	var value2 = 'Universe';
	<span id='@value'>@value2</span>
	<text>
		The sentence is equals to:
		@value from TJ
		<span>@value2</span>
		@value2 
		Testing
	</text>
}
<text>What @value</text>
";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithNestedTextElement() {
			var template = @"
@{
	var value = 'Hello World';
	var value2 = 'Universe';
	<span id='@value'>@value2</span>
	<text>
		The sentence is equals to:
		@value from TJ
		<span>@value2</span>
		@value2 
		Testing
		<text>Hello World to</text>
	</text>
}
<text>What @value</text>
";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithHelpers() {
			var template = @"
@{
	<span>@specialRender('Olamide')</span>
	<span>@specialRender('TJ')</span>
	<span>@specialRender('Dara')</span>
}

@helper specialRender(x){
	<span>Hello World to @x</span><br/>
}";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithNestedExpressionAndHtmlInBlocks() {
			var template = @"
@{
	var bar = 'Bar';
	var foo = 'Foo';
	<span>@foo. @bar.</span>
	@{
		bar += ' Another';
		foo += bar;
		<p>@bar</p>
		@{
			<span>@foo. @bar.</span>
		}
	}
}
<span>@foo. @bar.</span>
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionWithEscapeRazorCharacter() {
			var template = @"
@@
@@Test
<span>@@@@</span>
";
			var result = RazorJs.Parse(template, new { ID = "Tester" });

			Assert.IsTrue(result.Contains("@@"));
		}

		[TestMethod]
		public void CanParseExpressionInParam() {
			var template = @"
@{
	var myVariable = 10;
}
@(Html.Write(""Test"" + myVariable))
<br/>
@(Html.Write(""Test"" + myVariable))
";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseHtmlWriteMethodInLine() {
			var template = @"
@{
	var myVariable = 10;
}
@{ Html.Write(""Test"" + myVariable); }
<br/>
@{Html.Write(""Test"" + myVariable); }
";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseCodeBlock() {
			var template = @"
@{ 
	var moo = 'test';
	var foo = 'foo';
	var fooFunc = function(x){
		foo = x;
	}
}
<br/>
@Button.Create(function(s) { s.Text(moo); }).Render(this);        
			";

			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseExpressionInTag() {
			var template = @"
@{
	var Variable = 'Test';
}
<span>@Variable</span>
			";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseInlineMethodCallExpression() {
			var template = @"
<span>Testing</span>
@Button.Create('Testing').Render()
<span>@Test('ok')</span>
<br/>
";
			TestTemplate(template);
		}

		[TestMethod]
		public void CanParseMultilineMethodInvocation() {
			var template = @"
<span>Testing</span>
@Button.Create(function(setting){
	setting.Name = 'Test';
}).Render(this)
<span>Test2</span>
";
			TestTemplate(template);
		}
	}
}
