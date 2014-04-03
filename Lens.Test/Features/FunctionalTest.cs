﻿using NUnit.Framework;

namespace Lens.Test.Features
{
	[TestFixture]
	class FunctionalTest : TestBase
	{
		[Test]
		public void CreateFunctionObjectFromName()
		{
			var src = @"
var fx = double::IsInfinity
fx (1.0 / 0)";

			Test(src, true, true);
		}

		[Test]
		public void DelegateCasting()
		{
			var src = @"
var ts = (-> Console::WriteLine 1) as ThreadStart
ts ()";
			Test(src, null);
		}

		[Test]
		public void DelegateCasting2()
		{
			var src = @"
var filter = (x:int -> x > 2) as Predicate<int>
var arr = new [1; 2; 3; 4; 5]
Array::FindAll arr filter";

			Test(src, new[] { 3, 4, 5 });
		}

		[Test]
		public void Closure1()
		{
			var src = @"
var a = 0
var b = 2
var fx = x:int -> a = b * x
fx 3
a";
			Test(src, 6);
		}

		[Test]
		public void Closure2()
		{
			var src = @"
var result = 0
var x1 = 1
var fx1 = a:int ->
    x1 = x1 + 1
    var x2 = 1
    var fx2 = b:int ->
        x2 = x2 + 1
        result = x1 + x2 + b
    fx2 a
fx1 10
result";
			Test(src, 14);
		}

		[Test]
		public void FunctionComposition1()
		{
			var src = @"
let add = (a:int b:int) -> a + b
let square = x:int -> x ** 2 as int
let inc = x:int -> x + 1

let asi = add :> square :> inc
asi 1 2
";

			Test(src, 10);
		}

		[Test]
		public void FunctionComposition2()
		{
			var src = @"
let invConcat = (x:string y:string) -> y + x
let invParse = invConcat :> int::Parse
invParse ""37"" ""13""
";

			Test(src, 1337);
		}

		[Test]
		public void FunctionComposition3()
		{
			var src = @"
fun invConcat:string (x:string y:string) -> y + x
let invParse = invConcat :> int::Parse
invParse ""37"" ""13""
";

			Test(src, 1337);
		}

		[Test]
		public void FunctionComposition4()
		{
			var src = @"
let coeff = 2
let fx = int::Parse<string> :> (x:int -> x + coeff)
fx ""5""
";

			Test(src, 7);
		}

		[Test]
		public void Wildcards1()
		{
			var src = @"
let fx1 = string::Join <_, string[]> as object
let fx2 = string::Join <_, _, _, _> as object
let fx3 = int::Parse <_> as object
new [fx1; fx2; fx3]
    |> Where (x:object -> x <> null)
    |> Count ()
";
			Test(src, 3);
		}


		[Test]
		public void PartialApplication()
		{
			var src = @"
fun add:int (x:int y:int) -> x + y
let add2 = add 2 _
let add3 = add _ 3
(add2 1) + (add3 4)
";
			Test(src, 10);
		}
	}
}
