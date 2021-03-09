using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Svg2VectorDrawable
{
	class VdParser
	{
		const string PATH_SHIFT_X = "shift-x";
		const string PATH_SHIFT_Y = "shift-y";

		const string SHAPE_VECTOR = "vector";
		const string SHAPE_PATH = "path";
		const string SHAPE_GROUP = "group";

		const string PATH_ID = "android:name";
		const string PATH_DESCRIPTION = "android:pathData";
		const string PATH_FILL = "android:fillColor";
		const string PATH_FILL_OPACTIY = "android:fillAlpha";
		const string PATH_STROKE = "android:strokeColor";
		const string PATH_STROKE_OPACTIY = "android:strokeAlpha";

		const string PATH_STROKE_WIDTH = "android:strokeWidth";
		const string PATH_ROTATION = "android:rotation";
		const string PATH_ROTATION_X = "android:pivotX";
		const string PATH_ROTATION_Y = "android:pivotY";
		const string PATH_TRIM_START = "android:trimPathStart";
		const string PATH_TRIM_END = "android:trimPathEnd";
		const string PATH_TRIM_OFFSET = "android:trimPathOffset";
		const string PATH_STROKE_LINECAP = "android:strokeLinecap";
		const string PATH_STROKE_LINEJOIN = "android:strokeLinejoin";
		const string PATH_STROKE_MITERLIMIT = "android:strokeMiterlimit";
		const string PATH_CLIP = "android:clipToPath";
		const string LINECAP_BUTT = "butt";
		const string LINECAP_ROUND = "round";
		const string LINECAP_SQUARE = "square";
		const string LINEJOIN_MITER = "miter";
		const string LINEJOIN_ROUND = "round";
		const string LINEJOIN_BEVEL = "bevel";

		//interface IElemParser
		//{
		//	void Parse(VdTree path, XmlAttributeCollection attributes);
		//}

		//class ElemParser : IElemParser
		//{
		//	public ElemParser(Action<VdTree, XmlAttributeCollection> parser)
		//		=> Parser = parser;

		//	public Action<VdTree, XmlAttributeCollection> Parser { get; private set; }

		//	public void Parse(VdTree path, XmlAttributeCollection attributes)
		//		=> Parser?.Invoke(path, attributes);
		//}

		//Dictionary<string, ElemParser> tagSwitch = new Dictionary<string, ElemParser>
		//{
		//	{ SHAPE_VECTOR, new ElemParser((VdTree tree, XmlAttributeCollection attr)
		//		=> ParseSize(tree, attr)) },
		//	{ SHAPE_PATH, new ElemParser((VdTree tree, XmlAttributeCollection attr)
		//		=> tree.Add(ParsePathAttributes(attr))) },
		//	{ SHAPE_GROUP, new ElemParser((VdTree tree, XmlAttributeCollection attr)
		//		=> tree.Add(ParseGroupAttributes(attr))) }
		//};

		static int NextStart(String s, int end)
		{
			char c;

			while (end < s.Length)
			{
				c = s[end];
				// Note that 'e' or 'E' are not valid path commands, but could be
				// used for floating point numbers' scientific notation.
				// Therefore, when searching for next command, we should ignore 'e'
				// and 'E'.
				if ((((c - 'A') * (c - 'Z') <= 0) || ((c - 'a') * (c - 'z') <= 0))
						&& c != 'e' && c != 'E')
				{
					return end;
				}
				end++;
			}
			return end;
		}

		public static VdPath.Node[] ParsePath(string value)
		{
			int start = 0;
			int end = 1;

			var list = new List<VdPath.Node>();
			while (end < value.Length)
			{
				end = NextStart(value, end);
				var s = value.JavaSubstring(start, end);
				float[] val = GetFloats(s);

				AddNode(list, s[0], val);

				start = end;
				end++;
			}
			if ((end - start) == 1 && start < value.Length)
			{
				AddNode(list, value[start], new float[0]);
			}
			return list.ToArray();
		}

		class ExtractFloatResult
		{
			// We need to return the position of the next separator and whether the
			// next float starts with a '-' or a '.'.
			public int EndPosition { get; set; }
			public bool EndWithNegOrDot { get; set; }
		}

		/**
		 * Copies elements from {@code original} into a new array, from indexes start (inclusive) to
		 * end (exclusive). The original order of elements is preserved.
		 * If {@code end} is greater than {@code original.length}, the result is padded
		 * with the value {@code 0.0f}.
		 *
		 * @param original the original array
		 * @param start the start index, inclusive
		 * @param end the end index, exclusive
		 * @return the new array
		 * @throws ArrayIndexOutOfBoundsException if {@code start < 0 || start > original.length}
		 * @throws IllegalArgumentException if {@code start > end}
		 * @throws NullPointerException if {@code original == null}
		 */
		static float[] CopyOfRange(float[] original, int start, int end)
		{
			if (start > end)
			{
				throw new ArgumentOutOfRangeException();
			}
			int originalLength = original.Length;
			if (start < 0 || start > originalLength)
			{
				throw new ArgumentOutOfRangeException();
			}
			int resultLength = end - start;
			int copyLength = Math.Min(resultLength, originalLength - start);
			float[] result = new float[resultLength];
			Array.Copy(original, result, resultLength);
			return result;
		}

		/**
		 * Calculate the position of the next comma or space or negative sign
		 * @param s the string to search
		 * @param start the position to start searching
		 * @param result the result of the extraction, including the position of the
		 * the starting position of next number, whether it is ending with a '-'.
		 */
		static void Extract(string s, int start, ExtractFloatResult result)
		{
			// Now looking for ' ', ',', '.' or '-' from the start.
			int currentIndex = start;
			bool foundSeparator = false;
			result.EndWithNegOrDot = false;
			bool secondDot = false;
			bool isExponential = false;
			for (; currentIndex < s.Length; currentIndex++)
			{
				var isPrevExponential = isExponential;
				isExponential = false;
				char currentChar = s[currentIndex];
				switch (currentChar)
				{
					case ' ':
					case ',':
						foundSeparator = true;
						break;
					case '-':
						// The negative sign following a 'e' or 'E' is not a separator.
						if (currentIndex != start && !isPrevExponential)
						{
							foundSeparator = true;
							result.EndWithNegOrDot = true;
						}
						break;
					case '.':
						if (!secondDot)
						{
							secondDot = true;
						}
						else
						{
							// This is the second dot, and it is considered as a separator.
							foundSeparator = true;
							result.EndWithNegOrDot = true;
						}
						break;
					case 'e':
					case 'E':
						isExponential = true;
						break;
				}
				if (foundSeparator)
				{
					break;
				}
			}
			// When there is nothing found, then we put the end position to the end
			// of the string.
			result.EndPosition = currentIndex;
		}

		/**
		 * parse the floats in the string this is an optimized version of parseFloat(s.split(",|\\s"));
		 *
		 * @param s the string containing a command and list of floats
		 * @return array of floats
		 */
		static float[] GetFloats(string s)
		{
			if (s[0] == 'z' || s[0] == 'Z')
			{
				return new float[0];
			}
			try
			{
				float[] results = new float[s.Length];
				int count = 0;
				int startPosition = 1;
				int endPosition = 0;

				var result = new ExtractFloatResult();
				int totalLength = s.Length;

				// The startPosition should always be the first character of the
				// current number, and endPosition is the character after the current
				// number.
				while (startPosition < totalLength)
				{
					Extract(s, startPosition, result);
					endPosition = result.EndPosition;

					if (startPosition < endPosition)
					{
						var fstr = s.JavaSubstring(startPosition, endPosition);

						results[count++] = float.Parse(fstr.TrimEnd(','), CultureInfo.InvariantCulture);
					}

					if (result.EndWithNegOrDot)
					{
						// Keep the '-' or '.' sign with next number.
						startPosition = endPosition;
					}
					else
					{
						startPosition = endPosition + 1;
					}
				}
				return CopyOfRange(results, 0, count);
			}
			catch (Exception e)
			{
				throw new Exception("error in parsing \"" + s + "\"", e);
			}
		}
		// End of copy from PathParser.java
		////////////////////////////////////////////////////////////////
		static void AddNode(List<VdPath.Node> list, char cmd, float[] val)
		{
			list.Add(new VdPath.Node(cmd, val));
		}

		static void ParseSize(VdTree vdTree, XmlAttributeCollection attributes)
		{
			var pattern = new Regex("^\\s*(\\d+(\\.\\d+)*)\\s*([a-zA-Z]+)\\s*$", RegexOptions.Compiled);
			var m = new Dictionary<string, int>
		{
			{ "px", 1 },
			{ "dip", 1 },
			{ "dp", 1 },
			{ "sp", 1 },
			{ "pt", 1 },
			{ "in", 1 },
			{ "mm", 1 }
		};

			int len = attributes.Count;

			for (int i = 0; i < len; i++)
			{
				String name = attributes[i].Name;
				String value = attributes[i].Value;
				var matcher = pattern.Match(value);
				float size = 0;
				if (matcher.Success)
				{
					float v = float.Parse(matcher.Groups[1].Value, CultureInfo.InvariantCulture);
					var unit = matcher.Groups[3].Value.ToLowerInvariant();
					size = v;
				}
				// -- Extract dimension units.

				if ("android:width".Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					vdTree.BaseWidth = size;
				}
				else if ("android:height".Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					vdTree.BaseHeight = size;
				}
				else if ("android:viewportWidth".Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					vdTree.PortWidth = float.Parse(value, CultureInfo.InvariantCulture);
				}
				else if ("android:viewportHeight".Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					vdTree.PortHeight = float.Parse(value, CultureInfo.InvariantCulture);
				}
				else if ("android:alpha".Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					vdTree.RootAlpha = float.Parse(value, CultureInfo.InvariantCulture);
				}
				else
				{
					continue;
				}

			}
		}

		static VdPath ParsePathAttributes(XmlAttributeCollection attributes)
		{
			int len = attributes.Count;
			VdPath vgPath = new VdPath();

			for (int i = 0; i < len; i++)
			{
				String name = attributes[i].Name;
				String value = attributes[i].Value;
				//logger.log(Level.FINE, "name " + name + "value " + value);
				SetNameValue(vgPath, name, value);
			}
			return vgPath;
		}

		static VdGroup ParseGroupAttributes(XmlAttributeCollection attributes)
		{
			int len = attributes.Count;
			var vgGroup = new VdGroup();

			for (int i = 0; i < len; i++)
			{
				String name = attributes[i].Name;
				String value = attributes[i].Value;
				//logger.log(Level.FINE, "name " + name + "value " + value);
			}
			return vgGroup;
		}

		public static void SetNameValue(VdPath vgPath, String name, String value)
		{
			if (PATH_DESCRIPTION.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.Nodes = ParsePath(value);
			}
			else if (PATH_ID.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.Name = value;
			}
			else if (PATH_FILL.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.FillColor = CalculateColor(value);
				if (!float.IsNaN(vgPath.FillOpacity))
				{
					vgPath.FillColor &= 0x00FFFFFF;
					vgPath.FillColor |= ((uint)(0xFF * vgPath.FillOpacity)) << 24;
				}
			}
			else if (PATH_STROKE.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.StrokeColor = CalculateColor(value);
				if (!float.IsNaN(vgPath.StrokeOpacity))
				{
					vgPath.StrokeColor &= 0x00FFFFFF;
					vgPath.StrokeColor |= ((uint)(0xFF * vgPath.StrokeOpacity)) << 24;
				}
			}
			else if (PATH_FILL_OPACTIY.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.FillOpacity = float.Parse(value, CultureInfo.InvariantCulture);
				vgPath.FillColor &= 0x00FFFFFF;
				vgPath.FillColor |= ((uint)(0xFF * vgPath.FillOpacity)) << 24;
			}
			else if (PATH_STROKE_OPACTIY.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.StrokeOpacity = float.Parse(value, CultureInfo.InvariantCulture);
				vgPath.StrokeColor &= 0x00FFFFFF;
				vgPath.StrokeColor |= ((uint)(0xFF * vgPath.StrokeOpacity)) << 24;
			}
			else if (PATH_STROKE_WIDTH.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.StrokeWidth = float.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (PATH_ROTATION.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.Rotate = float.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (PATH_SHIFT_X.Equals(name))
			{
				vgPath.ShiftX = float.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (PATH_SHIFT_Y.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.ShiftY = float.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (PATH_ROTATION_Y.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.RotateY = float.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (PATH_ROTATION_X.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.RotateX = float.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (PATH_CLIP.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.Clip = bool.Parse(value);
			}
			else if (PATH_TRIM_START.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.TrimPathStart = float.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (PATH_TRIM_END.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.TrimPathEnd = float.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (PATH_TRIM_OFFSET.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.TrimPathOffset = float.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (PATH_STROKE_LINECAP.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				if (LINECAP_BUTT.Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					vgPath.StrokeLineCap = 0;
				}
				else if (LINECAP_ROUND.Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					vgPath.StrokeLineCap = 1;
				}
				else if (LINECAP_SQUARE.Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					vgPath.StrokeLineCap = 2;
				}
			}
			else if (PATH_STROKE_LINEJOIN.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				if (LINEJOIN_MITER.Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					vgPath.StrokeLineJoin = 0;
				}
				else if (LINEJOIN_ROUND.Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					vgPath.StrokeLineJoin = 1;
				}
				else if (LINEJOIN_BEVEL.Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					vgPath.StrokeLineJoin = 2;
				}
			}
			else if (PATH_STROKE_MITERLIMIT.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				vgPath.StrokeMiterlimit = float.Parse(value, CultureInfo.InvariantCulture);
			}
			else
			{
				//logger.log(Level.FINE, ">>>>>> DID NOT UNDERSTAND ! \"" + name + "\" <<<<");
			}

		}

		static uint CalculateColor(String value)
		{
			int len = value.Length;
			uint ret;
			uint k = 0;
			switch (len)
			{
				case 7: // #RRGGBB
						// Parse base16?
					ret = (uint)Convert.ToInt64(value.Substring(1), 16);
					//ret = (int)long.Parse(value.Substring(1), 16);
					ret |= 0xFF000000;
					break;
				case 9: // #AARRGGBB
					ret = (uint)Convert.ToInt64(value.Substring(1), 16);
					//ret = (int)long.parseLong(value.Substring(1), 16);
					break;
				case 4: // #RGB
					ret = (uint)Convert.ToInt64(value.Substring(1), 16);
					//ret = (int)long.parseLong(value.Substring(1), 16);

					k |= ((ret >> 8) & 0xF) * 0x110000;
					k |= ((ret >> 4) & 0xF) * 0x1100;
					k |= ((ret) & 0xF) * 0x11;
					ret = k | 0xFF000000;
					break;
				case 5: // #ARGB
					ret = (uint)Convert.ToInt64(value.Substring(1), 16);
					//ret = (int)Long.parseLong(value.Substring(1), 16);
					k |= ((ret >> 16) & 0xF) * 0x11000000;
					k |= ((ret >> 8) & 0xF) * 0x110000;
					k |= ((ret >> 4) & 0xF) * 0x1100;
					k |= ((ret) & 0xF) * 0x11;
					break;
				default:
					return 0xFF000000;
			}

			//logger.log(Level.FINE, "color = " + value + " = " + Integer.toHexString(ret));
			return ret;
		}
	}
}
