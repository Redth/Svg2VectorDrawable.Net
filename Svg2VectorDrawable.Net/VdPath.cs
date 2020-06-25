using System;
using System.Collections.Generic;
using System.Text;

namespace Svg2VectorDrawable
{
	class VdPath : VdElement
	{
		public Node[] Nodes { get; set; } = null;
		public uint StrokeColor { get; set; } = 0;
		public uint FillColor { get; set; } = 0;
		public float StrokeWidth { get; set; } = 0;
		public float Rotate { get; set; } = 0;
		public float ShiftX { get; set; } = 0;
		public float ShiftY { get; set; } = 0;
		public float RotateX { get; set; } = 0;
		public float RotateY { get; set; } = 0;
		public float TrimPathStart { get; set; } = 0;
		public float TrimPathEnd { get; set; } = 1;
		public float TrimPathOffset { get; set; } = 0;
		public int StrokeLineCap { get; set; } = -1;
		public int StrokeLineJoin { get; set; } = -1;
		public float StrokeMiterlimit { get; set; } = -1;
		public bool Clip { get; set; } = false;
		public float StrokeOpacity { get; set; } = float.NaN;
		public float FillOpacity { get; set; } = float.NaN;
		
		//public void toPath(Path2D path)
		//{
		//	path.reset();
		//	if (mNode != null)
		//	{
		//		VdNodeRender.creatPath(mNode, path);
		//	}
		//}

		public class Node
		{
			internal char type;
			internal float[] parameters;

			public Node(char type, float[] parameters)
			{
				this.type = type;
				this.parameters = parameters;
			}

			public Node(Node n)
			{
				this.type = n.type;
				this.parameters = new float[n.parameters.Length];
				Array.Copy(n.parameters, this.parameters, n.parameters.Length);
			}

			public static string NodeListToString(Node[] nodes)
			{
				var s = string.Empty;
				for (var i = 0; i < nodes.Length; i++)
				{
					Node n = nodes[i];
					s += n.type;
					int len = n.parameters.Length;
					for (int j = 0; j < len; j++)
					{
						if (j > 0)
						{
							s += ((j & 1) == 1) ? "," : " ";
						}
						// To avoid trailing zeros like 17.0, use this trick
						float value = n.parameters[j];
						if (value == (long)value)
						{
							s += ((long)value).ToString();
						}
						else
						{
							s += value.ToString();
						}

					}
				}
				return s;
			}

			public static void transform(float a,
					float b,
					float c,
					float d,
					float e,
					float f,
					Node[] nodes)
			{
				float[] pre = new float[2];
				for (int i = 0; i < nodes.Length; i++)
				{
					nodes[i].transform(a, b, c, d, e, f, pre);
				}
			}

			public void transform(float a,
					float b,
					float c,
					float d,
					float e,
					float f,
					float[] pre)
			{
				int incr = 0;
				float[] tempParams;
				float[] origParams;
				switch (type)
				{

					case 'z':
					case 'Z':
						return;
					case 'M':
					case 'L':
					case 'T':
						incr = 2;
						pre[0] = parameters[parameters.Length -2];
						pre[1] = parameters[parameters.Length -1];
						for (var i = 0; i < parameters.Length; i += incr) {
							Matrix(a, b, c, d, e, f, i, i + 1);
						}
						break;
					case 'm':
					case 'l':
					case 't':
						incr = 2;
						pre[0] += parameters[parameters.Length -2];
						pre[1] += parameters[parameters.Length -1];
						for (var i = 0; i < parameters.Length; i += incr) {
							Matrix(a, b, c, d, 0, 0, i, i + 1);
						}
						break;
					case 'h':
						type = 'l';
						pre[0] += parameters[parameters.Length -1];

						tempParams = new float[parameters.Length * 2];
						origParams = parameters;
						parameters = tempParams;
						for (int i = 0; i < parameters.Length; i += 2) {
							parameters[i] = origParams[i / 2];
							parameters[i +1] = 0;
							Matrix(a, b, c, d, 0, 0, i, i + 1);
						}

						break;
					case 'H':
						type = 'L';
						pre[0] = parameters[parameters.Length -1];
						tempParams = new float[parameters.Length * 2];
						origParams = parameters;
						parameters = tempParams;
						for (int i = 0; i < parameters.Length; i += 2) {
							parameters[i] = origParams[i / 2];
							parameters[i +1] = pre[1];
							Matrix(a, b, c, d, e, f, i, i + 1);
						}
						break;
					case 'v':
						pre[1] += parameters[parameters.Length -1];
						type = 'l';
						tempParams = new float[parameters.Length * 2];
						origParams = parameters;
						parameters = tempParams;
						for (int i = 0; i < parameters.Length; i += 2) {
							parameters[i] = 0;
							parameters[i +1] = origParams[i / 2];
							Matrix(a, b, c, d, 0, 0, i, i + 1);
						}
						break;
					case 'V':
						type = 'L';
						pre[1] = parameters[parameters.Length -1];
						tempParams = new float[parameters.Length * 2];
						origParams = parameters;
						parameters = tempParams;
						for (int i = 0; i < parameters.Length; i += 2) {
							parameters[i] = pre[0];
							parameters[i +1] = origParams[i / 2];
							Matrix(a, b, c, d, e, f, i, i + 1);
						}
						break;
					case 'C':
					case 'S':
					case 'Q':
						pre[0] = parameters[parameters.Length -2];
						pre[1] = parameters[parameters.Length -1];
						for (int i = 0; i < parameters.Length; i += 2) {
							Matrix(a, b, c, d, e, f, i, i + 1);
						}
						break;
					case 's':
					case 'q':
					case 'c':
						pre[0] += parameters[parameters.Length -2];
						pre[1] += parameters[parameters.Length -1];
						for (int i = 0; i < parameters.Length; i += 2) {
							Matrix(a, b, c, d, 0, 0, i, i + 1);
						}
						break;
					case 'a':
						incr = 7;
						pre[0] += parameters[parameters.Length -2];
						pre[1] += parameters[parameters.Length -1];
						for (int i = 0; i < parameters.Length; i += incr) {
							Matrix(a, b, c, d, 0, 0, i, i + 1);
							double ang = DegreesToRadians(parameters[i + 2]);
							parameters[i +2] = (float)RadiansToDegrees(ang + Math.Atan2(b, d));
							Matrix(a, b, c, d, 0, 0, i + 5, i + 6);
						}
						break;
					case 'A':
						incr = 7;
						pre[0] = parameters[parameters.Length -2];
						pre[1] = parameters[parameters.Length -1];
						for (int i = 0; i < parameters.Length; i += incr) {
							Matrix(a, b, c, d, e, f, i, i + 1);
							double ang = DegreesToRadians(parameters[i + 2]);
							parameters[i +2] = (float)RadiansToDegrees(ang + Math.Atan2(b, d));
							Matrix(a, b, c, d, e, f, i + 5, i + 6);
						}
						break;

				}
			}

			double DegreesToRadians(double degrees)
				=> degrees * (Math.PI / 180);

			double RadiansToDegrees(double radians)
				=> radians * (180 / Math.PI);

			void Matrix(float a,
					float b,
					float c,
					float d,
					float e,
					float f,
					int offx,
					int offy)
			{
				float inx = (offx < 0) ? 1 : parameters[offx];
				float iny = (offy < 0) ? 1 : parameters[offy];
				float x = inx * a + iny * c + e;
				float y = inx * b + iny * d + f;
				if (offx >= 0)
				{
					parameters[offx] = x;
				}
				if (offy >= 0)
				{
					parameters[offy] = y;
				}
			}
		}

		public override string Name { get; set; } = Guid.NewGuid().ToString();

		double Hypotenuse(double a, double b)
				=> Math.Sqrt(Math.Pow(a, 2) + Math.Pow(b, 2));

		/**
		 * TODO: support rotation attribute for stroke width
		 */
		public void Transform(float a, float b, float c, float d, float e, float f)
		{
			StrokeWidth *= (float)Hypotenuse(a + b, c + d);
			Node.transform(a, b, c, d, e, f, Nodes);
		}
	}
}
