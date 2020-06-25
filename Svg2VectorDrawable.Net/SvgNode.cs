using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Svg2VectorDrawable
{
	public abstract class SvgNode
	{
		public string Name { get; protected set; }

		// Keep a reference to the tree in order to dump the error log.
		public SvgTree SvgTree { get; private set; }

		// Use document node to get the line number for error reporting.
		public XmlNode DocumentNode { get; private set; }

		public SvgNode(SvgTree svgTree, XmlNode node, string name)
		{
			Name = name;
			SvgTree = svgTree;
			DocumentNode = node;
		}

		/**
         * dump the current node's debug info.
         */
		public abstract void DumpNode(String indent);

		/**
         * Write the Node content into the VectorDrawable's XML file.
         */
		public abstract void WriteXml(StreamWriter writer);

		/**
         * @return true the node is a group node.
         */
		public abstract bool IsGroupNode { get; }

		/**
         * Transform the current Node with the transformation matrix.
         */
		public abstract void Transform(float a, float b, float c, float d, float e, float f);
	}
}
