using System;
using System.Collections.Generic;
using System.Text;

namespace Svg2VectorDrawable
{
    class VdGroup : VdElement
    {
        public override string Name { get; set; } = Guid.NewGuid().ToString();

        public void Add(VdElement pathOrGroup)
            => Children.Add(pathOrGroup);

        // Children can be either a {@link VdPath} or {@link VdGroup}
        public List<VdElement> Children { get; } = new List<VdElement>();

        public int Count
            => Children?.Count ?? 0;
    }
}
