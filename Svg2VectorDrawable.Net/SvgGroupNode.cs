using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Svg2VectorDrawable
{
	public class SvgGroupNode : SvgNode
	{
		const string INDENT_LEVEL = "    ";
		List<SvgNode> children = new List<SvgNode>();

		public SvgGroupNode(SvgTree svgTree, XmlNode docNode, string name)
			: base(svgTree, docNode, name)
		{
		}

		public void AddChild(SvgNode child)
			=> children.Add(child);

		public override void DumpNode(string indent)
		{
			// Print the current group.
			// logger.log(Level.FINE, indent + "current group is :" + getName());

			// Then print all the children.
			foreach (var node in children)
			{
				node.DumpNode(indent + INDENT_LEVEL);
			}
		}

		public override bool IsGroupNode
			=> true;

		public override void Transform(float a, float b, float c, float d, float e, float f)
		{
			foreach (var p in children)
				p.Transform(a, b, c, d, e, f);
		}

		public override void WriteXml(StreamWriter writer)
		{
			foreach (var node in children)
				node.WriteXml(writer);
		}
	}
}
