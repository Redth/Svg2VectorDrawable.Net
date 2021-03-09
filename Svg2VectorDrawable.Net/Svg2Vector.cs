using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Svg2VectorDrawable
{
	public class Svg2Vector
	{
		internal const string SVG_POLYGON = "polygon";
		internal const string SVG_RECT = "rect";
		internal const string SVG_CIRCLE = "circle";
		internal const string SVG_LINE = "line";
		internal const string SVG_PATH = "path";
		internal const string SVG_GROUP = "g";
		internal const string SVG_TRANSFORM = "transform";
		internal const string SVG_WIDTH = "width";
		internal const string SVG_HEIGHT = "height";
		internal const string SVG_VIEW_BOX = "viewBox";
		internal const string SVG_STYLE = "style";
		internal const string SVG_DISPLAY = "display";

		internal const string SVG_D = "d";
		internal const string SVG_STROKE_COLOR = "stroke";
		internal const string SVG_STROKE_OPACITY = "stroke-opacity";
		internal const string SVG_STROKE_LINEJOINE = "stroke-linejoin";
		internal const string SVG_STROKE_LINECAP = "stroke-linecap";
		internal const string SVG_STROKE_WIDTH = "stroke-width";
		internal const string SVG_FILL_COLOR = "fill";
		internal const string SVG_FILL_OPACITY = "fill-opacity";
		internal const string SVG_OPACITY = "opacity";
		internal const string SVG_CLIP = "clip";
		internal const string SVG_POINTS = "points";

		static internal readonly Dictionary<string, string> presentationMap = new Dictionary<string, string>
			{
				{ SVG_STROKE_COLOR, "android:strokeColor" },
				{ SVG_STROKE_OPACITY, "android:strokeAlpha" },
				{ SVG_STROKE_LINEJOINE, "android:strokeLinejoin" },
				{ SVG_STROKE_LINECAP, "android:strokeLinecap" },
				{ SVG_STROKE_WIDTH, "android:strokeWidth" },
				{ SVG_FILL_COLOR, "android:fillColor" },
				{ SVG_FILL_OPACITY, "android:fillAlpha" },
				{ SVG_CLIP, "android:clip" },
				{ SVG_OPACITY, "android:fillAlpha" }
			};

		// List all the Svg nodes that we don't support. Categorized by the types.
		static readonly HashSet<string> unsupportedSvgNodes = new HashSet<string> {
			// Animation elements
			"animate", "animateColor", "animateMotion", "animateTransform", "mpath", "set",
			// Container elements
			"a", "defs", "glyph", "marker", "mask", "missing-glyph", "pattern", "switch", "symbol",
			// Filter primitive elements
			"feBlend", "feColorMatrix", "feComponentTransfer", "feComposite", "feConvolveMatrix",
			"feDiffuseLighting", "feDisplacementMap", "feFlood", "feFuncA", "feFuncB", "feFuncG",
			"feFuncR", "feGaussianBlur", "feImage", "feMerge", "feMergeNode", "feMorphology",
			"feOffset", "feSpecularLighting", "feTile", "feTurbulence",
			// Font elements
			"font", "font-face", "font-face-format", "font-face-name", "font-face-src", "font-face-uri",
			"hkern", "vkern",
			// Gradient elements
			"linearGradient", "radialGradient", "stop",
			// Graphics elements
			"ellipse", "polyline", "text", "use",
			// Light source elements
			"feDistantLight", "fePointLight", "feSpotLight",
			// Structural elements
			"defs", "symbol", "use",
			// Text content elements
			"altGlyph", "altGlyphDef", "altGlyphItem", "glyph", "glyphRef", "textPath", "text", "tref",
			"tspan",
			// Text content child elements
			"altGlyph", "textPath", "tref", "tspan",
			// Uncategorized elements
			"clipPath", "color-profile", "cursor", "filter", "foreignObject", "script", "view"
		};

		static SvgTree Parse(string file)
		{
			SvgTree svgTree = new SvgTree();
			XmlDocument doc = svgTree.Parse(file);
			XmlNodeList nSvgNode;

			// Parse svg elements
			nSvgNode = doc.GetElementsByTagName("svg");
			if (nSvgNode.Count != 1)
			{
				throw new InvalidOperationException("Not a proper SVG file");
			}
			var rootNode = nSvgNode.Item(0);
			for (int i = 0; i < nSvgNode.Count; i++)
			{
				var nNode = nSvgNode.Item(i);
				if (nNode.NodeType == XmlNodeType.Element)
				{
					ParseDimension(svgTree, nNode);
				}
			}

			if (svgTree.ViewBox == null)
			{
				svgTree.LogErrorLine("Missing \"viewBox\" in <svg> element", rootNode, SvgTree.SvgLogLevel.Error);
				return svgTree;
			}

			if ((svgTree.Width == 0 || svgTree.Height == 0) && svgTree.ViewBox[2] > 0 && svgTree.ViewBox[3] > 0)
			{
				svgTree.Width = svgTree.ViewBox[2];
				svgTree.Height = svgTree.ViewBox[3];
			}

			// Parse transformation information.
			// TODO: Properly handle transformation in the group level. In the "use" case, we treat
			// it as global for now.
			XmlNodeList nUseTags;
			svgTree.Matrix = new float[6];
			svgTree.Matrix[0] = 1;
			svgTree.Matrix[3] = 1;

			nUseTags = doc.GetElementsByTagName("use");
			for (int temp = 0; temp < nUseTags.Count; temp++)
			{
				var nNode = nUseTags.Item(temp);
				if (nNode.NodeType == XmlNodeType.Element)
				{
					ParseTransformation(svgTree, nNode);
				}
			}

			SvgGroupNode root = new SvgGroupNode(svgTree, rootNode, "root");
			svgTree.Root = root;

			// Parse all the group and path node recursively.
			TraverseSvgAndExtract(svgTree, root, rootNode);

			svgTree.Dump(root);

			return svgTree;
		}

		static void TraverseSvgAndExtract(SvgTree svgTree, SvgGroupNode currentGroup, XmlNode item)
		{
			// Recursively traverse all the group and path nodes
			XmlNodeList allChildren = item.ChildNodes;

			for (int i = 0; i < allChildren.Count; i++)
			{
				var currentNode = allChildren.Item(i);
				String nodeName = currentNode.Name;
				if (SVG_PATH.Equals(nodeName, StringComparison.OrdinalIgnoreCase) ||
					SVG_RECT.Equals(nodeName, StringComparison.OrdinalIgnoreCase) ||
					SVG_CIRCLE.Equals(nodeName, StringComparison.OrdinalIgnoreCase) ||
					SVG_POLYGON.Equals(nodeName, StringComparison.OrdinalIgnoreCase) ||
					SVG_LINE.Equals(nodeName, StringComparison.OrdinalIgnoreCase))
				{
					SvgLeafNode child = new SvgLeafNode(svgTree, currentNode, nodeName + i);

					ExtractAllItemsAs(svgTree, child, currentNode);

					currentGroup.AddChild(child);
				}
				else if (SVG_GROUP.Equals(nodeName, StringComparison.OrdinalIgnoreCase))
				{
					SvgGroupNode childGroup = new SvgGroupNode(svgTree, currentNode, "child" + i);
					currentGroup.AddChild(childGroup);
					TraverseSvgAndExtract(svgTree, childGroup, currentNode);
				}
				else
				{
					// For other fancy tags, like <refs>, they can contain children too.
					// Report the unsupported nodes.
					if (unsupportedSvgNodes.Contains(nodeName.ToLowerInvariant()))
					{
						svgTree.LogErrorLine("<" + nodeName + "> is not supported", currentNode,
											 SvgTree.SvgLogLevel.Error);
					}
					TraverseSvgAndExtract(svgTree, currentGroup, currentNode);
				}
			}

		}

		static void ParseTransformation(SvgTree avg, XmlNode nNode)
		{
			var a = nNode.Attributes;
			int len = a.Count;

			for (int i = 0; i < len; i++)
			{
				var n = a.Item(i);
				String name = n.Name;
				String value = n.Value;
				if (SVG_TRANSFORM.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					if (value.StartsWith("matrix(", StringComparison.OrdinalIgnoreCase))
					{
						value = value.JavaSubstring("matrix(".Length, value.Length - 1);
						var sp = value.Split(' ');
						for (int j = 0; j < sp.Length; j++)
						{
							avg.Matrix[j] = float.Parse(sp[j], CultureInfo.InvariantCulture);
						}
					}
				}
				else if (name.Equals("y", StringComparison.OrdinalIgnoreCase))
				{
					// TODO: Do something with this value?
					float.Parse(value, CultureInfo.InvariantCulture);
				}
				else if (name.Equals("x", StringComparison.OrdinalIgnoreCase))
				{
					// TODO: Do something with this value?
					float.Parse(value, CultureInfo.InvariantCulture);
				}

			}
		}

		static void ParseDimension(SvgTree avg, XmlNode nNode)
		{
			var a = nNode.Attributes;
			int len = a.Count;

			float? percentWidth = null;
			float? percentHeight = null;

			for (int i = 0; i < len; i++)
			{
				var n = a.Item(i);
				var name = n.Name;
				var value = n.Value;
				int subStringSize = value.Length;
				if (subStringSize > 2)
				{
					if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
					{
						subStringSize = subStringSize - 2;
					}
				}

				if (SVG_WIDTH.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					if (value.EndsWith("%", StringComparison.OrdinalIgnoreCase))
						percentWidth = float.Parse(value.JavaSubstring(0, subStringSize - 1), CultureInfo.InvariantCulture);
					else
						avg.Width = float.Parse(value.JavaSubstring(0, subStringSize), CultureInfo.InvariantCulture);
				}
				else if (SVG_HEIGHT.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					if (value.EndsWith("%", StringComparison.OrdinalIgnoreCase))
						percentHeight = float.Parse(value.JavaSubstring(0, subStringSize - 1), CultureInfo.InvariantCulture);
					else
						avg.Height = float.Parse(value.JavaSubstring(0, subStringSize), CultureInfo.InvariantCulture);
				}
				else if (SVG_VIEW_BOX.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					avg.ViewBox = new float[4];
					String[] strbox = value.Split(' ');
					for (int j = 0; j < avg.ViewBox.Length; j++)
					{
						avg.ViewBox[j] = float.Parse(strbox[j], CultureInfo.InvariantCulture);
					}
				}
			}
			if (avg.ViewBox == null)
			{
				avg.ViewBox = new float[4];
				avg.ViewBox[2] = avg.Width;
				avg.ViewBox[3] = avg.Height;
			}
			if (avg.ViewBox != null)
			{
				if (percentWidth != null)
					avg.Width = avg.ViewBox[2] * (percentWidth.Value / 100.0f);
				if (percentHeight != null)
					avg.Height = avg.ViewBox[3] * (percentHeight.Value / 100.0f);
			}
		}

		// Read the content from currentItem, and fill into "child"
		static void ExtractAllItemsAs(SvgTree avg, SvgLeafNode child, XmlNode currentItem)
		{
			var currentGroup = currentItem.ParentNode;

			bool hasNodeAttr = false;
			String styleContent = "";
			bool nothingToDisplay = false;

			while (currentGroup != null && currentGroup.Name.Equals("g", StringComparison.OrdinalIgnoreCase))
			{
				// Parse the group's attributes.
				//logger.log(Level.FINE, "Printing current parent");
				PrintLnCommon(currentGroup);

				var attr = currentGroup.Attributes;
				var nodeAttr = attr.GetNamedItem(SVG_STYLE);
				// Search for the "display:none", if existed, then skip this item.
				if (nodeAttr != null)
				{
					styleContent += nodeAttr.InnerText + ";";
					//logger.log(Level.FINE, "styleContent is :" + styleContent + "at number group ");
					if (styleContent.Contains("display:none"))
					{
						//logger.log(Level.FINE, "Found none style, skip the whole group");
						nothingToDisplay = true;
						break;
					}
					else
					{
						hasNodeAttr = true;
					}
				}

				var displayAttr = attr.GetNamedItem(SVG_DISPLAY);
				if (displayAttr != null && "none".Equals(displayAttr.Value, StringComparison.OrdinalIgnoreCase))
				{
					//logger.log(Level.FINE, "Found display:none style, skip the whole group");
					nothingToDisplay = true;
					break;
				}
				currentGroup = currentGroup.ParentNode;
			}

			if (nothingToDisplay)
			{
				// Skip this current whole item.
				return;
			}

			//logger.log(Level.FINE, "Print current item");
			PrintLnCommon(currentItem);

			if (hasNodeAttr && styleContent != null)
			{
				AddStyleToPath(child, styleContent);
			}

			var currentGroupNode = currentItem;

			if (SVG_PATH.Equals(currentGroupNode.Name, StringComparison.OrdinalIgnoreCase))
			{
				ExtractPathItem(avg, child, currentGroupNode);
			}

			if (SVG_RECT.Equals(currentGroupNode.Name, StringComparison.OrdinalIgnoreCase))
			{
				ExtractRectItem(avg, child, currentGroupNode);
			}

			if (SVG_CIRCLE.Equals(currentGroupNode.Name, StringComparison.OrdinalIgnoreCase))
			{
				ExtractCircleItem(avg, child, currentGroupNode);
			}

			if (SVG_POLYGON.Equals(currentGroupNode.Name, StringComparison.OrdinalIgnoreCase))
			{
				ExtractPolyItem(avg, child, currentGroupNode);
			}

			if (SVG_LINE.Equals(currentGroupNode.Name, StringComparison.OrdinalIgnoreCase))
			{
				ExtractLineItem(avg, child, currentGroupNode);
			}
		}

		static void PrintLnCommon(XmlNode n)
		{
			//logger.log(Level.FINE, " nodeName=\"" + n.getNodeName() + "\"");

			var val = n.NamespaceURI;
			if (val != null)
			{
				//logger.log(Level.FINE, " uri=\"" + val + "\"");
			}

			val = n.Prefix;

			if (val != null)
			{
				//logger.log(Level.FINE, " pre=\"" + val + "\"");
			}

			val = n.LocalName;
			if (val != null)
			{
				//logger.log(Level.FINE, " local=\"" + val + "\"");
			}

			val = n.Value;
			if (val != null)
			{
				//logger.log(Level.FINE, " nodeValue=");
				if (string.IsNullOrEmpty(val.Trim()))
				{
					// Whitespace
					//logger.log(Level.FINE, "[WS]");
				}
				else
				{
					//logger.log(Level.FINE, "\"" + n.getNodeValue() + "\"");
				}
			}
		}

		/**
		 * Convert polygon element into a path.
		 */
		static void ExtractPolyItem(SvgTree avg, SvgLeafNode child, XmlNode currentGroupNode)
		{
			//logger.log(Level.FINE, "Rect found" + currentGroupNode.getTextContent());
			if (currentGroupNode.NodeType == XmlNodeType.Element)
			{

				var a = currentGroupNode.Attributes;
				int len = a.Count;

				for (int itemIndex = 0; itemIndex < len; itemIndex++)
				{
					var n = a.Item(itemIndex);
					String name = n.Name;
					String value = n.Value;
					if (name.Equals(SVG_STYLE, StringComparison.OrdinalIgnoreCase))
					{
						AddStyleToPath(child, value);
					}
					else if (presentationMap.ContainsKey(name))
					{
						child.FillPresentationAttributes(name, value);
					}
					else if (name.Equals(SVG_POINTS, StringComparison.OrdinalIgnoreCase))
					{
						PathBuilder builder = new PathBuilder();
						var split = Regex.Split(value, "[\\s,]+");
						float baseX = float.Parse(split[0], CultureInfo.InvariantCulture);
						float baseY = float.Parse(split[1], CultureInfo.InvariantCulture);
						builder.AbsoluteMoveTo(baseX, baseY);
						for (int j = 2; j < split.Length; j += 2)
						{
							float x = float.Parse(split[j], CultureInfo.InvariantCulture);
							float y = float.Parse(split[j + 1], CultureInfo.InvariantCulture);
							builder.RelativeLineTo(x - baseX, y - baseY);
							baseX = x;
							baseY = y;
						}
						builder.RelativeClose();
						child.SetPathData(builder.ToString());
					}
				}
			}
		}

		/**
		 * Convert rectangle element into a path.
		 */
		static void ExtractRectItem(SvgTree avg, SvgLeafNode child, XmlNode currentGroupNode)
		{
			//logger.log(Level.FINE, "Rect found" + currentGroupNode.getTextContent());

			if (currentGroupNode.NodeType == XmlNodeType.Element)
			{
				float x = 0;
				float y = 0;
				float width = float.NaN;
				float height = float.NaN;

				var a = currentGroupNode.Attributes;
				int len = a.Count;
				bool pureTransparent = false;
				for (int j = 0; j < len; j++)
				{
					var n = a.Item(j);
					String name = n.Name;
					String value = n.Value;
					if (name.Equals(SVG_STYLE, StringComparison.OrdinalIgnoreCase))
					{
						AddStyleToPath(child, value);
						if (value.Contains("opacity:0;"))
						{
							pureTransparent = true;
						}
					}
					else if (presentationMap.ContainsKey(name))
					{
						child.FillPresentationAttributes(name, value);
					}
					else if (name.Equals("clip-path", StringComparison.OrdinalIgnoreCase) && value.StartsWith("url(#SVGID_", StringComparison.OrdinalIgnoreCase))
					{

					}
					else if (name.Equals("x"))
					{
						x = float.Parse(value, CultureInfo.InvariantCulture);
					}
					else if (name.Equals("y", StringComparison.OrdinalIgnoreCase))
					{
						y = float.Parse(value, CultureInfo.InvariantCulture);
					}
					else if (name.Equals("width", StringComparison.OrdinalIgnoreCase))
					{
						width = float.Parse(value, CultureInfo.InvariantCulture);
					}
					else if (name.Equals("height", StringComparison.OrdinalIgnoreCase))
					{
						height = float.Parse(value, CultureInfo.InvariantCulture);
					}
					else if (name.Equals("style", StringComparison.OrdinalIgnoreCase))
					{

					}

				}

				if (!pureTransparent && avg != null && !float.IsNaN(x) && !float.IsNaN(y)
						&& !float.IsNaN(width)
						&& !float.IsNaN(height))
				{
					// "M x, y h width v height h -width z"
					PathBuilder builder = new PathBuilder();
					builder.AbsoluteMoveTo(x, y);
					builder.RelativeHorizontalTo(width);
					builder.RelativeVerticalTo(height);
					builder.RelativeHorizontalTo(-width);
					builder.RelativeClose();
					child.SetPathData(builder.ToString());
				}
			}
		}

		/**
		 * Convert circle element into a path.
		 */
		static void ExtractCircleItem(SvgTree avg, SvgLeafNode child, XmlNode currentGroupNode)
		{
			//logger.log(Level.FINE, "circle found" + currentGroupNode.getTextContent());

			if (currentGroupNode.NodeType == XmlNodeType.Element)
			{
				float cx = 0;
				float cy = 0;
				float radius = 0;

				var a = currentGroupNode.Attributes;
				int len = a.Count;
				bool pureTransparent = false;
				for (int j = 0; j < len; j++)
				{
					var n = a.Item(j);
					String name = n.Name;
					String value = n.Value;
					if (name.Equals(SVG_STYLE, StringComparison.OrdinalIgnoreCase))
					{
						AddStyleToPath(child, value);
						if (value.Contains("opacity:0;"))
						{
							pureTransparent = true;
						}
					}
					else if (presentationMap.ContainsKey(name))
					{
						child.FillPresentationAttributes(name, value);
					}
					else if (name.Equals("clip-path", StringComparison.OrdinalIgnoreCase) && value.StartsWith("url(#SVGID_", StringComparison.OrdinalIgnoreCase))
					{

					}
					else if (name.Equals("cx", StringComparison.OrdinalIgnoreCase))
					{
						cx = float.Parse(value, CultureInfo.InvariantCulture);
					}
					else if (name.Equals("cy", StringComparison.OrdinalIgnoreCase))
					{
						cy = float.Parse(value, CultureInfo.InvariantCulture);
					}
					else if (name.Equals("r", StringComparison.OrdinalIgnoreCase))
					{
						radius = float.Parse(value, CultureInfo.InvariantCulture);
					}

				}

				if (!pureTransparent && avg != null && !float.IsNaN(cx) && !float.IsNaN(cy))
				{
					// "M cx cy m -r, 0 a r,r 0 1,1 (r * 2),0 a r,r 0 1,1 -(r * 2),0"
					PathBuilder builder = new PathBuilder();
					builder.AbsoluteMoveTo(cx, cy);
					builder.RelativeMoveTo(-radius, 0);
					builder.RelativeArcTo(radius, radius, false, true, true, 2 * radius, 0);
					builder.RelativeArcTo(radius, radius, false, true, true, -2 * radius, 0);
					child.SetPathData(builder.ToString());
				}
			}
		}

		/**
		 * Convert line element into a path.
		 */
		static void ExtractLineItem(SvgTree avg, SvgLeafNode child, XmlNode currentGroupNode)
		{
			//logger.log(Level.FINE, "line found" + currentGroupNode.getTextContent());

			if (currentGroupNode.NodeType == XmlNodeType.Element)
			{
				float x1 = 0;
				float y1 = 0;
				float x2 = 0;
				float y2 = 0;

				var a = currentGroupNode.Attributes;
				int len = a.Count;
				bool pureTransparent = false;
				for (int j = 0; j < len; j++)
				{
					var n = a.Item(j);
					String name = n.Name;
					String value = n.Value;
					if (name.Equals(SVG_STYLE, StringComparison.OrdinalIgnoreCase))
					{
						AddStyleToPath(child, value);
						if (value.Contains("opacity:0;"))
						{
							pureTransparent = true;
						}
					}
					else if (presentationMap.ContainsKey(name))
					{
						child.FillPresentationAttributes(name, value);
					}
					else if (name.Equals("clip-path", StringComparison.OrdinalIgnoreCase) && value.StartsWith("url(#SVGID_", StringComparison.OrdinalIgnoreCase))
					{
						// TODO: Handle clip path here.
					}
					else if (name.Equals("x1", StringComparison.OrdinalIgnoreCase))
					{
						x1 = float.Parse(value, CultureInfo.InvariantCulture);
					}
					else if (name.Equals("y1", StringComparison.OrdinalIgnoreCase))
					{
						y1 = float.Parse(value, CultureInfo.InvariantCulture);
					}
					else if (name.Equals("x2", StringComparison.OrdinalIgnoreCase))
					{
						x2 = float.Parse(value, CultureInfo.InvariantCulture);
					}
					else if (name.Equals("y2", StringComparison.OrdinalIgnoreCase))
					{
						y2 = float.Parse(value, CultureInfo.InvariantCulture);
					}
				}

				if (!pureTransparent && avg != null && !float.IsNaN(x1) && !float.IsNaN(y1)
						&& !float.IsNaN(x2) && !float.IsNaN(y2))
				{
					// "M x1, y1 L x2, y2"
					PathBuilder builder = new PathBuilder();
					builder.AbsoluteMoveTo(x1, y1);
					builder.AbsoluteLineTo(x2, y2);
					child.SetPathData(builder.ToString());
				}
			}

		}

		static void ExtractPathItem(SvgTree avg, SvgLeafNode child, XmlNode currentGroupNode)
		{
			//logger.log(Level.FINE, "Path found " + currentGroupNode.getTextContent());

			if (currentGroupNode.NodeType == XmlNodeType.Element)
			{
				var eElement = (XmlElement)currentGroupNode;

				var a = currentGroupNode.Attributes;
				int len = a.Count;

				for (int j = 0; j < len; j++)
				{
					var n = a.Item(j);
					String name = n.Name;
					String value = n.Value;
					if (name.Equals(SVG_STYLE, StringComparison.OrdinalIgnoreCase))
					{
						AddStyleToPath(child, value);
					}
					else if (presentationMap.ContainsKey(name))
					{
						child.FillPresentationAttributes(name, value);
					}
					else if (name.Equals(SVG_D, StringComparison.OrdinalIgnoreCase))
					{
						var pathData = Regex.Replace(value, "(\\d)-", "$1,-");
						child.SetPathData(pathData);
					}

				}
			}
		}

		static void AddStyleToPath(SvgLeafNode path, String value)
		{
			//logger.log(Level.FINE, "Style found is " + value);
			if (value != null)
			{
				String[] parts = value.Split(';');
				for (int k = parts.Length - 1; k >= 0; k--)
				{
					String subStyle = parts[k];
					String[] nameValue = subStyle.Split(':');
					if (nameValue.Length == 2 && nameValue[0] != null && nameValue[1] != null)
					{
						if (presentationMap.ContainsKey(nameValue[0]))
						{
							path.FillPresentationAttributes(nameValue[0], nameValue[1]);
						}
						else if (nameValue[0].Equals(SVG_OPACITY, StringComparison.OrdinalIgnoreCase))
						{
							// TODO: This is hacky, since we don't have a group level
							// android:opacity. This only works when the path didn't overlap.
							path.FillPresentationAttributes(SVG_FILL_OPACITY, nameValue[1]);
						}
					}
				}
			}
		}

		const string head = "<vector xmlns:android=\"http://schemas.android.com/apk/res/android\"\n";

		static string GetSizeString(float w, float h, float scaleFactor)
			=> "        android:width=\"" + (int)(w * scaleFactor) + "dp\"\n" +
				"        android:height=\"" + (int)(h * scaleFactor) + "dp\"\n";

		static void WriteFile(Stream outStream, SvgTree svgTree)
		{
			var fw = new StreamWriter(outStream);
			fw.Write(head);
			float finalWidth = svgTree.Width;
			float finalHeight = svgTree.Height;

			fw.Write(GetSizeString(finalWidth, finalHeight, svgTree.ScaleFactor));

			fw.Write("        android:viewportWidth=\"" + svgTree.Width + "\"\n");
			fw.Write("        android:viewportHeight=\"" + svgTree.Height + "\">\n");

			svgTree.Normalize();
			// TODO: this has to happen in the tree mode!!!
			WriteXml(svgTree, fw);
			fw.Write("</vector>\n");
			fw.Close();
		}

		static void WriteXml(SvgTree svgTree, StreamWriter fw)
			=> svgTree.Root.WriteXml(fw);

		public static string Convert(string inputSvgFilename, string outputXmlFilename)
		{
			using (var s = File.Create(outputXmlFilename))
				return ParseSvgToXml(inputSvgFilename, s);
		}

		/**
		 * Convert a SVG file into VectorDrawable's XML content, if no error is found.
		 *
		 * @param inputSVG the input SVG file
		 * @param outStream the converted VectorDrawable's content. This can be
		 *                  empty if there is any error found during parsing
		 * @return the error messages, which contain things like all the tags
		 *         VectorDrawble don't support or exception message.
		 */
		public static String ParseSvgToXml(string inputSvgFilename, Stream outputStream)
		{
			// Write all the error message during parsing into SvgTree. and return here as getErrorLog().
			// We will also log the exceptions here.
			String errorLog = null;
			try
			{
				SvgTree svgTree = Parse(inputSvgFilename);
				errorLog = svgTree.GetErrorLog();
				// When there was anything in the input SVG file that we can't
				// convert to VectorDrawable, we logged them as errors.
				// After we logged all the errors, we skipped the XML file generation.
				if (svgTree.CanConvertToVectorDrawable())
				{
					WriteFile(outputStream, svgTree);
				}
			}
			catch (Exception e)
			{
				errorLog = "EXCEPTION in parsing " + inputSvgFilename + ":\n" + e.Message;
			}
			return errorLog;
		}
		
		static internal readonly Dictionary<string, string> htmlColorMap = new Dictionary<string, string>
			{
				{ "transparent", "#00000000" },
				{ "clear", "#00000000" },
				{ "aliceblue", "#f0f8ff" },
				{ "antiquewhite", "#faebd7" },
				{ "aqua", "#00ffff" },
				{ "aquamarine", "#7fffd4" },
				{ "azure", "#f0ffff" },
				{ "beige", "#f5f5dc" },
				{ "bisque", "#ffe4c4" },
				{ "black", "#000000" },
				{ "blanchedalmond", "#ffebcd" },
				{ "blue", "#0000ff" },
				{ "blueviolet", "#8a2be2" },
				{ "brown", "#a52a2a" },
				{ "burlywood", "#deb887" },
				{ "cadetblue", "#5f9ea0" },
				{ "chartreuse", "#7fff00" },
				{ "chocolate", "#d2691e" },
				{ "coral", "#ff7f50" },
				{ "cornflowerblue", "#6495ed" },
				{ "cornsilk", "#fff8dc" },
				{ "crimson", "#dc143c" },
				{ "cyan", "#00ffff" },
				{ "darkblue", "#00008b" },
				{ "darkcyan", "#008b8b" },
				{ "darkgoldenrod", "#b8860b" },
				{ "darkgray", "#a9a9a9" },
				{ "darkgrey", "#a9a9a9" },
				{ "darkgreen", "#006400" },
				{ "darkkhaki", "#bdb76b" },
				{ "darkmagenta", "#8b008b" },
				{ "darkolivegreen", "#556b2f" },
				{ "darkorange", "#ff8c00" },
				{ "darkorchid", "#9932cc" },
				{ "darkred", "#8b0000" },
				{ "darksalmon", "#e9967a" },
				{ "darkseagreen", "#8fbc8f" },
				{ "darkslateblue", "#483d8b" },
				{ "darkslategray", "#2f4f4f" },
				{ "darkslategrey", "#2f4f4f" },
				{ "darkturquoise", "#00ced1" },
				{ "darkviolet", "#9400d3" },
				{ "deeppink", "#ff1493" },
				{ "deepskyblue", "#00bfff" },
				{ "dimgray", "#696969" },
				{ "dimgrey", "#696969" },
				{ "dodgerblue", "#1e90ff" },
				{ "firebrick", "#b22222" },
				{ "floralwhite", "#fffaf0" },
				{ "forestgreen", "#228b22" },
				{ "fuchsia", "#ff00ff" },
				{ "gainsboro", "#dcdcdc" },
				{ "ghostwhite", "#f8f8ff" },
				{ "gold", "#ffd700" },
				{ "goldenrod", "#daa520" },
				{ "gray", "#808080" },
				{ "grey", "#808080" },
				{ "green", "#008000" },
				{ "greenyellow", "#adff2f" },
				{ "honeydew", "#f0fff0" },
				{ "hotpink", "#ff69b4" },
				{ "indianred", "#cd5c5c" },
				{ "indigo", "#4b0082" },
				{ "ivory", "#fffff0" },
				{ "khaki", "#f0e68c" },
				{ "lavender", "#e6e6fa" },
				{ "lavenderblush", "#fff0f5" },
				{ "lawngreen", "#7cfc00" },
				{ "lemonchiffon", "#fffacd" },
				{ "lightblue", "#add8e6" },
				{ "lightcoral", "#f08080" },
				{ "lightcyan", "#e0ffff" },
				{ "lightgoldenrodyellow", "#fafad2" },
				{ "lightgray", "#d3d3d3" },
				{ "lightgrey", "#d3d3d3" },
				{ "lightgreen", "#90ee90" },
				{ "lightpink", "#ffb6c1" },
				{ "lightsalmon", "#ffa07a" },
				{ "lightseagreen", "#20b2aa" },
				{ "lightskyblue", "#87cefa" },
				{ "lightslategray", "#778899" },
				{ "lightslategrey", "#778899" },
				{ "lightsteelblue", "#b0c4de" },
				{ "lightyellow", "#ffffe0" },
				{ "lime", "#00ff00" },
				{ "limegreen", "#32cd32" },
				{ "linen", "#faf0e6" },
				{ "magenta", "#ff00ff" },
				{ "maroon", "#800000" },
				{ "mediumaquamarine", "#66cdaa" },
				{ "mediumblue", "#0000cd" },
				{ "mediumorchid", "#ba55d3" },
				{ "mediumpurple", "#9370db" },
				{ "mediumseagreen", "#3cb371" },
				{ "mediumslateblue", "#7b68ee" },
				{ "mediumspringgreen", "#00fa9a" },
				{ "mediumturquoise", "#48d1cc" },
				{ "mediumvioletred", "#c71585" },
				{ "midnightblue", "#191970" },
				{ "mintcream", "#f5fffa" },
				{ "mistyrose", "#ffe4e1" },
				{ "moccasin", "#ffe4b5" },
				{ "navajowhite", "#ffdead" },
				{ "navy", "#000080" },
				{ "oldlace", "#fdf5e6" },
				{ "olive", "#808000" },
				{ "olivedrab", "#6b8e23" },
				{ "orange", "#ffa500" },
				{ "orangered", "#ff4500" },
				{ "orchid", "#da70d6" },
				{ "palegoldenrod", "#eee8aa" },
				{ "palegreen", "#98fb98" },
				{ "paleturquoise", "#afeeee" },
				{ "palevioletred", "#db7093" },
				{ "papayawhip", "#ffefd5" },
				{ "peachpuff", "#ffdab9" },
				{ "peru", "#cd853f" },
				{ "pink", "#ffc0cb" },
				{ "plum", "#dda0dd" },
				{ "powderblue", "#b0e0e6" },
				{ "purple", "#800080" },
				{ "rebeccapurple", "#663399" },
				{ "red", "#ff0000" },
				{ "rosybrown", "#bc8f8f" },
				{ "royalblue", "#4169e1" },
				{ "saddlebrown", "#8b4513" },
				{ "salmon", "#fa8072" },
				{ "sandybrown", "#f4a460" },
				{ "seagreen", "#2e8b57" },
				{ "seashell", "#fff5ee" },
				{ "sienna", "#a0522d" },
				{ "silver", "#c0c0c0" },
				{ "skyblue", "#87ceeb" },
				{ "slateblue", "#6a5acd" },
				{ "slategray", "#708090" },
				{ "slategrey", "#708090" },
				{ "snow", "#fffafa" },
				{ "springgreen", "#00ff7f" },
				{ "steelblue", "#4682b4" },
				{ "tan", "#d2b48c" },
				{ "teal", "#008080" },
				{ "thistle", "#d8bfd8" },
				{ "tomato", "#ff6347" },
				{ "turquoise", "#40e0d0" },
				{ "violet", "#ee82ee" },
				{ "wheat", "#f5deb3" },
				{ "white", "#ffffff" },
				{ "whitesmoke", "#f5f5f5" },
				{ "yellow", "#ffff00" },
				{ "yellowgreen", "#9acd32" },
			};
	}
}
