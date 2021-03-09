using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Svg2VectorDrawable
{
	class SvgLeafNode : SvgNode
	{
		string pathData;

		// Key is the attributes for vector drawable, and the value is the converted from SVG.
		Dictionary<string, string> vdAttributesMap = new Dictionary<string, string>();

		public SvgLeafNode(SvgTree svgTree, XmlNode node, String nodeName)
				: base(svgTree, node, nodeName)
		{
		}

		string GetAttributeValues(Dictionary<string, string> presentationMap)
		{
			var sb = new StringBuilder("/>\n");
			foreach (var key in vdAttributesMap.Keys)
			{
				var vectorDrawableAttr = presentationMap[key];
				var svgValue = vdAttributesMap[key];
				var vdValue = svgValue.Trim();

				// There are several cases we need to convert from SVG format to
				// VectorDrawable format. Like "none", "3px" or "rgb(255, 0, 0)"
				if ("none".Equals(vdValue, StringComparison.OrdinalIgnoreCase))
				{
					vdValue = "#00000000";
				}
				else if (vdValue.EndsWith("px", StringComparison.OrdinalIgnoreCase))
				{
					vdValue = vdValue.JavaSubstring(0, vdValue.Length - 2);
				}
				else if (vdValue.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
				{
					vdValue = vdValue.JavaSubstring(3, vdValue.Length);
					vdValue = ConvertRgbToHex(vdValue);
					if (vdValue == null)
					{
						SvgTree.LogErrorLine("Unsupported Color format " + vdValue, DocumentNode,
											   SvgTree.SvgLogLevel.Error);
					}
				}
				else if (Svg2Vector.htmlColorMap.TryGetValue(vdValue.ToLowerInvariant(), out var htmlHex))
				{
					vdValue = htmlHex;
				}
				String attr = "\n        " + vectorDrawableAttr + "=\"" +
							  vdValue + "\"";
				sb.Insert(0, attr);

			}
			return sb.ToString();
		}

		public static int Clamp(int val, int min, int max)
			=> Math.Max(min, Math.Min(max, val));

		/**
		 * SVG allows using rgb(int, int, int) or rgb(float%, float%, float%) to
		 * represent a color, but Android doesn't. Therefore, we need to convert
		 * them into #RRGGBB format.
		 * @param svgValue in either "(int, int, int)" or "(float%, float%, float%)"
		 * @return #RRGGBB in hex format, or null, if an error is found.
		 */
		string ConvertRgbToHex(string svgValue)
		{
			// We don't support color keyword yet.
			// http://www.w3.org/TR/SVG11/types.html#ColorKeywords
			string result = null;
			var functionValue = svgValue.Trim();
			functionValue = svgValue.JavaSubstring(1, functionValue.Length - 1);
			// After we cut the "(", ")", we can deal with the numbers.
			var numbers = functionValue.Split(',');
			if (numbers.Length != 3)
			{
				return null;
			}
			var color = new byte[3];
			for (int i = 0; i < 3; i++)
			{
				String number = numbers[i];
				number = number.Trim();
				if (number.EndsWith("%", StringComparison.OrdinalIgnoreCase))
				{
					float value = float.Parse(number.JavaSubstring(0, number.Length - 1), CultureInfo.InvariantCulture);
					color[i] = (byte)Clamp((int)(value * 255.0f / 100.0f), 0, 255);
				}
				else
				{
					int value = int.Parse(number, CultureInfo.InvariantCulture);
					color[i] = (byte)Clamp(value, 0, 255);
				}
			}

			result = BitConverter.ToString(color).Replace("-", string.Empty);
			if (result.Length == 7)
				return result;
			return null;
		}

		public override void DumpNode(String indent)
		{
			//logger.log(Level.FINE, indent + (mPathData != null ? mPathData : " null pathData ") +
			//					   (mName != null ? mName : " null name "));
		}

		public void SetPathData(String pathData)
		{
			this.pathData = pathData;
		}

		public override bool IsGroupNode
			=> false;

		public override void Transform(float a, float b, float c, float d, float e, float f)
		{
			if (pathData == null)
				return;

			// Nothing to draw and transform, early return.
			if (vdAttributesMap.ContainsKey("fill") && vdAttributesMap["fill"].Equals("none", StringComparison.OrdinalIgnoreCase))
				return;

			// TODO: We need to just apply the transformation to group.
			var n = VdParser.ParsePath(pathData);
			if (!(a == 1 && d == 1 && b == 0 && c == 0 && e == 0 && f == 0))
			{
				VdPath.Node.transform(a, b, c, d, e, f, n);
			}
			pathData = VdPath.Node.NodeListToString(n);
		}

		public override void WriteXml(StreamWriter writer)
		{
			var fillColor = vdAttributesMap.ContainsKey(Svg2Vector.SVG_FILL_COLOR) ? vdAttributesMap[Svg2Vector.SVG_FILL_COLOR] : null;
			var strokeColor = vdAttributesMap.ContainsKey(Svg2Vector.SVG_STROKE_COLOR) ? vdAttributesMap[Svg2Vector.SVG_STROKE_COLOR] : null;
			//logger.log(Level.FINE, "fill color " + fillColor);

			var emptyFill = fillColor != null && ("none".Equals(fillColor, StringComparison.OrdinalIgnoreCase) || "#0000000".Equals(fillColor, StringComparison.OrdinalIgnoreCase));
			var emptyStroke = strokeColor == null || "none".Equals(strokeColor, StringComparison.OrdinalIgnoreCase);
			var emptyPath = pathData == null;
			var nothingToDraw = emptyPath || emptyFill && emptyStroke;
			if (nothingToDraw)
			{
				return;
			}

			writer.Write("    <path\n");
			if (!vdAttributesMap.ContainsKey(Svg2Vector.SVG_FILL_COLOR))
			{
				//logger.log(Level.FINE, "ADDING FILL SVG_FILL_COLOR");
				writer.Write("        android:fillColor=\"#FF000000\"\n");
			}
			writer.Write("        android:pathData=\"" + pathData + "\"");
			writer.Write(GetAttributeValues(Svg2Vector.presentationMap));
		}

		public void FillPresentationAttributes(String name, String value)
		{
			//logger.log(Level.FINE, ">>>> PROP " + name + " = " + value);
			if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
			{
				SvgTree.LogErrorLine("Unsupported URL value: " + value, DocumentNode,
									   SvgTree.SvgLogLevel.Error);
				return;
			}
			if (vdAttributesMap.ContainsKey(name))
				vdAttributesMap[name] = value;
			else
				vdAttributesMap.Add(name, value);
		}
	}
}
