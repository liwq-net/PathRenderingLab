﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace PathRenderingLab
{
    public struct FloatRectangle
    {
        public float X, Y, Width, Height;
        public FloatRectangle(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool Intersects(FloatRectangle o) => !(X > o.X + o.Width || o.X > X + Width || Y > o.Y + o.Height || o.Y > Y + Height);

        public override string ToString() => $"{X} {Y} {Width} {Height}";
    }

    public struct DoubleRectangle
    {
        public double X, Y, Width, Height;
        public DoubleRectangle(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool Intersects(DoubleRectangle o) => !(X > o.X + o.Width || o.X > X + Width || Y > o.Y + o.Height || o.Y > Y + Height);

        public override string ToString() => $"{X} {Y} {Width} {Height}";
    }

    public struct RootPair
    {
        public double A, B;
        public RootPair(double a, double b) { A = a; B = b; }

        public RootPair Flip() => new RootPair(B, A);
        public override string ToString() => $"{A} {B}";
    }

    public static class GeometricUtils
    {
        public static bool Inside01(double t) => t >= 0f && t <= 1f;

        public static IEnumerable<RootPair> Remap(this IEnumerable<RootPair> pair, double t1, double t2, double u1, double u2)
        {
            double Remap1(double t) => t1 + t * (t2 - t1);
            double Remap2(double u) => u1 + u * (u2 - u1);

            return pair.Select(p => new RootPair(Remap1(p.A), Remap2(p.B)));
        }

        public static bool SegmentsIntersect(Double2 p0, Double2 p1, Double2 q0, Double2 q1)
        {
            // Check if segments are inside interval
            bool InsideSegmentCollinear(Double2 x0, Double2 x1, Double2 y)
            {
                var d = (x1 - x0).Dot(y - x0);
                return d >= 0 && d <= (x1 - x0).LengthSquared;
            }

            // The cross products
            double crossq0 = (p1 - p0).Cross(q0 - p0);
            double crossq1 = (p1 - p0).Cross(q1 - p0);
            double crossp0 = (q1 - q0).Cross(p0 - q0);
            double crossp1 = (q1 - q0).Cross(p1 - q0);

            // If two points are equal, we have only containment
            if (DoubleUtils.RoughlyEquals(p0, p1))
                return DoubleUtils.RoughlyZero(crossp0) && InsideSegmentCollinear(q0, q1, p0);
            if (DoubleUtils.RoughlyEquals(q0, q1))
                return DoubleUtils.RoughlyZero(crossq0) && InsideSegmentCollinear(p0, p1, q0);

            // Containment
            if (DoubleUtils.RoughlyZero(crossq0)) return InsideSegmentCollinear(p0, p1, q0);
            if (DoubleUtils.RoughlyZero(crossq1)) return InsideSegmentCollinear(p0, p1, q1);
            if (DoubleUtils.RoughlyZero(crossp0)) return InsideSegmentCollinear(q0, q1, p0);
            if (DoubleUtils.RoughlyZero(crossp1)) return InsideSegmentCollinear(q0, q1, p1);

            // Check if everything is on one side
            if (crossq0 < 0 && crossq1 < 0) return false;
            if (crossq0 > 0 && crossq1 > 0) return false;
            if (crossp0 < 0 && crossp1 < 0) return false;
            if (crossp0 > 0 && crossp1 > 0) return false;

            // Otherwise...
            return true;
        }

        public static bool PolygonsOverlap(Double2[] poly0, Double2[] poly1)
        {
            // Check for segments intersection
            for (int j = 0; j < poly0.Length; j++)
                for (int i = 0; i < poly1.Length; i++)
                {
                    var p0 = poly0[j];
                    var q0 = poly1[i];

                    var p1 = poly0[j == 0 ? poly0.Length - 1 : j - 1];
                    var q1 = poly1[i == 0 ? poly1.Length - 1 : i - 1];

                    if (SegmentsIntersect(p0, p1, q0, q1)) return true;
                }

            // Check for overlapping of the first point
            if (PolygonContainsPoint(poly0, poly1[0]) || PolygonContainsPoint(poly1, poly0[0]))
                return true;

            // Otherwise...
            return false;
        }

        public static bool PolygonSegmentIntersect(Double2[] poly, Double2 a, Double2 b)
        {
            // Check for segments intersection
            for (int i = 0; i < poly.Length; i++)
            {
                var p0 = poly[i];
                var p1 = poly[i == 0 ? poly.Length - 1 : i - 1];

                if (SegmentsIntersect(p0, p1, a, b)) return true;
            }

            // Check for overlapping of the segment point
            if (PolygonContainsPoint(poly, a) || PolygonContainsPoint(poly, b))
                return true;

            // Otherwise...
            return false;
        }

        public static bool PolygonContainsPoint(Double2[] poly, Double2 p)
        {
            bool contains = false;

            for (int i = 0; i < poly.Length; i++)
            {
                var p0 = poly[i];
                var p1 = poly[i == 0 ? poly.Length - 1 : i - 1];

                if (p0.X < p.X && p1.X < p.X) continue;
                if (p0.X < p.X) p0 = p1 + (p.X - p1.X) / (p0.X - p1.X) * (p0 - p1);
                if (p1.X < p.X) p1 = p0 + (p.X - p0.X) / (p1.X - p0.X) * (p1 - p0);
                if ((p0.Y >= p.Y) != (p1.Y >= p.Y)) contains = !contains;
            }

            return contains;
        }

        public static Double2[] SimplifyPolygon(Double2[] polygon)
        {
            // Quickly discard degenerate polygons
            if (polygon.Length < 3) return polygon;

            // Find a non-collinear polygon first
            int istart;
            int len = polygon.Length;
            for (istart = 0; istart < len; istart++)
            {
                var ik = (istart + 1) % len;
                var ip = (istart + len - 1) % len;

                if (!DoubleUtils.RoughlyZero((polygon[ik] - polygon[istart]).Cross(polygon[ip] - polygon[istart]))) break;
            }

            // If there are no polygons non-collinear polygons, just return a line
            if (istart == len)
            {
                var imin = 0;
                var imax = 0;

                for (int i = 1; i < len; i++)
                {
                    if (polygon[imin].X > polygon[i].X) imin = i;
                    if (polygon[imax].X < polygon[i].X) imax = i;
                }

                return new[] { polygon[imin], polygon[imax] };
            }
            else
            {
                // Start with a single point
                var points = new List<Double2>(len) { polygon[istart] };
                Double2 LastAddedPoint() => points[points.Count - 1];

                for (int i = (istart + 1) % len; i != istart; i = (i + 1) % len)
                {
                    // Check for collinearity
                    var ik = (i + 1) % len;
                    var ip = (i + len - 1) % len;

                    // Add if the point isn't collinear
                    if (!DoubleUtils.RoughlyZero((polygon[ik] - polygon[i]).Cross(LastAddedPoint() - polygon[i])))
                        points.Add(polygon[i]);
                }

                // Return the new formed polygon
                return points.ToArray();
            }
        }

        public static IEnumerable<Double2[]> RemovePolygonWedges(Double2[] polygon)
        {
            // Quickly discard degenerate polygons
            if (polygon.Length < 3) return Enumerable.Empty<Double2[]>();

            // We will use a DCEL to do it
            var dcel = new DCEL.DCEL();

            for (int i = 0; i < polygon.Length; i++)
            {
                var ik = (i + 1) % polygon.Length;
                dcel.AddCurve(Curve.Line(polygon[i], polygon[ik]));
            }

            // Now, remove the wedges
            dcel.RemoveWedges();

            // And collect the faces
            return dcel.Faces.Where(f => !f.IsOuterFace).Select(f => f.Edges.Select(p => p.E1.Position).ToArray());
        }

        public static Double2[] CircleIntersection(Double2 c1, double r1, Double2 c2, double r2)
        {
            // Firstly, rotate the second circle so it stands on the X-axis
            var rot = (c2 - c1).Normalized;

            // Get the displacement
            var a = (c2 - c1).RotScale(rot.NegateY).X;
            var x = (r1 * r1 + a * a - r2 * r2) / (2 * a);
            var ys = r1 * r1 - x * x;

            // No intersection if this is negative
            if (ys < 0) return new Double2[0];
            else
            {
                var y = Math.Sqrt(ys);
                var pos = new Double2[] { new Double2(x, y), new Double2(x, -y) };

                // Project back to the new position
                return pos.Select(p => c1 + p.RotScale(rot)).ToArray();
            }
        }
    }
}