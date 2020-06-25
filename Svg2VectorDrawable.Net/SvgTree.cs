using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Svg2VectorDrawable
{
	public class SvgTree
	{
		public float Width { get; set; }
		public float Height { get; set; }
		public float[] Matrix { get; set; }
		public float[] ViewBox { get; set; }
		public float ScaleFactor { get; set; } = 1;

		public string Filename { get; private set; }

		List<string> errorLines = new List<string>();


		public enum SvgLogLevel
		{
			Error,
			Warning
		}

		public XmlDocument Parse(string filename)
		{
			Filename = filename;

			var doc = new XmlDocument();
			doc.Load(filename);
			return doc;
		}

		public void Normalize()
		{
			if (Matrix != null)
				Transform(Matrix[0], Matrix[1], Matrix[2], Matrix[3], Matrix[4], Matrix[5]);

			if (ViewBox != null && (ViewBox[0] != 0 || ViewBox[1] != 0))
				Transform(1, 0, 0, 1, -ViewBox[0], -ViewBox[1]);
		}

		private void Transform(float a, float b, float c, float d, float e, float f)
		{
			Root.Transform(a, b, c, d, e, f);
		}

		public void Dump(SvgGroupNode root)
		{
			// logger.log(Level.FINE, "current file is :" + mFileName);
			root.DumpNode("");
		}

		public SvgGroupNode Root { get; set; }
		
		public void LogErrorLine(string s, XmlNode node, SvgLogLevel level)
		{
			if (!string.IsNullOrEmpty(s))
			{
				if (node != null)
				{
					var position = getPosition(node);

					if (position != null && position.HasLineInfo())
						errorLines.Add(level.ToString() + "@ line " + (position.LineNumber + 1) + " " + s + "\n");
					else
						errorLines.Add(s);
				}
				else
				{
					errorLines.Add(s);
				}
			}
		}

		/**
		 * @return Error log. Empty string if there are no errors.
		 */
		public string GetErrorLog()
		{
			var errorBuilder = new StringBuilder();
			if (errorLines.Any())
			{
				errorBuilder.Append("In " + Filename + ":\n");
			}
			foreach (var log in errorLines)
			{
				errorBuilder.Append(log);
			}
			return errorBuilder.ToString();
		}

		/**
		 * @return true when there is no error found when parsing the SVG file.
		 */
		public bool CanConvertToVectorDrawable()
			=> !errorLines.Any();

		IXmlLineInfo getPosition(XmlNode node)
		{
			return null; // TODO: Get position
		}
	}
}
