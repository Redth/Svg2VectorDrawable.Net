using System;
using System.Collections.Generic;
using System.Text;

namespace Svg2VectorDrawable
{
	public static class Extensions
	{
		public static string JavaSubstring(this string s, int start, int end)
			=> s.Substring(start, end - start);
	}
}
