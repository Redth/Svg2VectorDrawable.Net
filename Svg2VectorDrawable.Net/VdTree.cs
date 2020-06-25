using System;
using System.Collections.Generic;
using System.Text;

namespace Svg2VectorDrawable
{
	class VdTree
	{
		VdGroup currentGroup = new VdGroup();
		List<VdElement> children = new List<VdElement>();

		public float BaseWidth { get; set; } = 1;
		public float BaseHeight { get; set; } = 1;
		public float PortWidth { get; set; } = 1;
		public float PortHeight { get; set; } = 1;
		public float RootAlpha { get; set; } = 1;

		/**
		* Ensure there is at least one animation for every path in group (linking
		* them by names) Build the "current" path based on the first group
		*/
		public void ParseFinish()
		{
			children = currentGroup.Children;
		}

		public void Add(VdElement pathOrGroup)
		{
			currentGroup.Add(pathOrGroup);
		}

		public float GetBaseWidth()
		{
			return BaseWidth;
		}

		public float GetBaseHeight()
		{
			return BaseHeight;
		}
	}
}
