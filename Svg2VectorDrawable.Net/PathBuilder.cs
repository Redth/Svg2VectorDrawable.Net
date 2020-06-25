
using System;
using System.Text;

namespace Svg2VectorDrawable
{
	internal class PathBuilder
	{
		StringBuilder pathData = new StringBuilder();

		string BoolToString(bool flag)
			=> flag ? "1" : "0";

		public PathBuilder AbsoluteMoveTo(float x, float y)
		{
			pathData.Append("M" + x + "," + y);
			return this;
		}

		public PathBuilder RelativeMoveTo(float x, float y)
		{
			pathData.Append("m" + x + "," + y);
			return this;
		}

		public PathBuilder AbsoluteLineTo(float x, float y)
		{
			pathData.Append("L" + x + "," + y);
			return this;
		}

		public PathBuilder RelativeLineTo(float x, float y)
		{
			pathData.Append("l" + x + "," + y);
			return this;
		}

		public PathBuilder AbsoluteVerticalTo(float v)
		{
			pathData.Append("V" + v);
			return this;
		}

		public PathBuilder RelativeVerticalTo(float v)
		{
			pathData.Append("v" + v);
			return this;
		}

		public PathBuilder absoluteHorizontalTo(float h)
		{
			pathData.Append("H" + h);
			return this;
		}

		public PathBuilder RelativeHorizontalTo(float h)
		{
			pathData.Append("h" + h);
			return this;
		}

		public PathBuilder AbsoluteArcTo(float rx, float ry, bool rotation, bool largeArc, bool sweep, float x, float y)
		{
			pathData.Append("A" + rx + "," + ry + "," + BoolToString(rotation) + "," +
							 BoolToString(largeArc) + "," + BoolToString(sweep) + "," + x + "," + y);
			return this;
		}

		public PathBuilder RelativeArcTo(float rx, float ry, bool rotation, bool largeArc, bool sweep, float x, float y)
		{
			pathData.Append("a" + rx + "," + ry + "," + BoolToString(rotation) + "," +
							 BoolToString(largeArc) + "," + BoolToString(sweep) + "," + x + "," + y);
			return this;
		}

		public PathBuilder AbsoluteClose()
		{
			pathData.Append("Z");
			return this;
		}

		public PathBuilder RelativeClose()
		{
			pathData.Append("z");
			return this;
		}

		public override string ToString()
			=> pathData.ToString();
	}
}
