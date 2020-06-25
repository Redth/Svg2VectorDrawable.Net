using System;

namespace Svg2VectorDrawable.Console
{
	class Program
	{
		static void Main(string[] args)
		{
			Svg2VectorDrawable.Svg2Vector.Convert("C:\\code\\xamagon.svg", "C:\\code\\xamagon.xml");
		}
	}
}
