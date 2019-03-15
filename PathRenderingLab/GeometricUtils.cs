﻿using System;
using System.Collections.Generic;
using System.Linq;
using static PathRenderingLab.DoubleUtils;

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

        public DoubleRectangle Truncate()
        {
            var x1 = X.Truncate();
            var y1 = Y.Truncate();
            var x2 = (X + Width).Truncate();
            var y2 = (Y + Height).Truncate();

            return new DoubleRectangle(x1, y1, x2 - x1, y2 - y1);
        }

        public override string ToString() => $"{X} {Y} {Width} {Height}";
    }

    public struct RootPair
    {
        public double A, B;
        public RootPair(double a, double b) { A = a; B = b; }

        public RootPair Flip() => new RootPair(B, A);
        public override string ToString() => $"{A} {B}";

        public bool Inside01() => GeometricUtils.Inside01(A) && GeometricUtils.Inside01(B);
    }

    /// <summary>
    /// A generalized comparer for vectors, used in geometric routines
    /// </summary>
    public class CanonicalComparer : Comparer<Double2>
    {
        public override int Compare(Double2 a, Double2 b) => a.Y == b.Y ? b.X.CompareTo(a.X) : a.Y.CompareTo(b.Y);
        public new readonly static CanonicalComparer Default = new CanonicalComparer();
    }

    public static class GeometricUtils
    {
        public static bool Inside01(double t) => t >= 0 && t <= 1;

        public static IEnumerable<RootPair> Remap(this IEnumerable<RootPair> pair, double t1, double t2, double u1, double u2)
        {
            double Remap1(double t) => t1 + t * (t2 - t1);
            double Remap2(double u) => u1 + u * (u2 - u1);

            return pair.Select(p => new RootPair(Remap1(p.A), Remap2(p.B)));
        }

        // Check if segments are inside interval
        public static bool InsideSegmentCollinear(Double2 x0, Double2 x1, Double2 y, bool strict = false)
        {
            var d = (x1 - x0).Dot(y - x0);
            return strict ? d > 0 && d < (x1 - x0).LengthSquared
                : d >= 0 && d <= (x1 - x0).LengthSquared;
        }

        public static bool SegmentsIntersect(Double2 p0, Double2 p1, Double2 q0, Double2 q1, bool strict = false)
        {
            // The cross products
            double crossq0 = (p1 - p0).Cross(q0 - p0);
            double crossq1 = (p1 - p0).Cross(q1 - p0);
            double crossp0 = (q1 - q0).Cross(p0 - q0);
            double crossp1 = (q1 - q0).Cross(p1 - q0);

            // If two points are equal, we have only containment (not considered in strict case)
            if (RoughlyEquals(p0, p1))
                return !strict && RoughlyZero(crossp0) && InsideSegmentCollinear(q0, q1, p0, strict);
            if (RoughlyEquals(q0, q1))
                return !strict && RoughlyZero(crossq0) && InsideSegmentCollinear(p0, p1, q0, strict);

            // Containment
            if (RoughlyZero(crossq0)) return InsideSegmentCollinear(p0, p1, q0, strict);
            if (RoughlyZero(crossq1)) return InsideSegmentCollinear(p0, p1, q1, strict);
            if (RoughlyZero(crossp0)) return InsideSegmentCollinear(q0, q1, p0, strict);
            if (RoughlyZero(crossp1)) return InsideSegmentCollinear(q0, q1, p1, strict);

            // Check if everything is on one side
            if (crossq0 < 0 && crossq1 < 0) return false;
            if (crossq0 > 0 && crossq1 > 0) return false;
            if (crossp0 < 0 && crossp1 < 0) return false;
            if (crossp0 > 0 && crossp1 > 0) return false;

            // Otherwise...
            return true;
        }

        public static bool PolygonsOverlap(Double2[] poly0, Double2[] poly1, bool strict = false)
        {
            // Check for segments intersection
            for (int j = 0; j < poly0.Length; j++)
                for (int i = 0; i < poly1.Length; i++)
                {
                    var p0 = poly0[j];
                    var q0 = poly1[i];

                    var p1 = poly0[j == 0 ? poly0.Length - 1 : j - 1];
                    var q1 = poly1[i == 0 ? poly1.Length - 1 : i - 1];

                    if (SegmentsIntersect(p0, p1, q0, q1, strict)) return true;
                }

            // Check for overlapping of the first point
            if (PolygonContainsPoint(poly0, poly1[0], strict) || PolygonContainsPoint(poly1, poly0[0], strict))
                return true;

            // Otherwise...
            return false;
        }

        public static bool PolygonSegmentIntersect(Double2[] poly, Double2 a, Double2 b, bool strict = false)
        {
            // Check for segments intersection
            for (int i = 0; i < poly.Length; i++)
            {
                var p0 = poly[i];
                var p1 = poly[i == 0 ? poly.Length - 1 : i - 1];

                if (SegmentsIntersect(p0, p1, a, b, strict)) return true;
            }

            // Check for overlapping of the segment point
            if (PolygonContainsPoint(poly, a, strict) || PolygonContainsPoint(poly, b, strict))
                return true;

            // Otherwise...
            return false;
        }

        public static bool PolygonContainsPoint(Double2[] poly, Double2 p, bool strict = false)
        {
            bool contains = false;

            for (int i = 0; i < poly.Length; i++)
            {
                var p0 = poly[i];
                var p1 = poly[i == 0 ? poly.Length - 1 : i - 1];

                // For strictness, if the line is "inside" the polygon, we have a problem
                if (strict && RoughlyZero((p1 - p0).Cross(p - p0)) &&
                    InsideSegmentCollinear(p0, p1, p, true)) return false;

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

                if (!RoughlyZero((polygon[ik] - polygon[istart]).Cross(polygon[ip] - polygon[istart]))) break;
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

                // Only add the point if it doesn't form a parallel line with the next point on the line
                for (int i = (istart + 1) % len; i != istart; i = (i + 1) % len)
                    if (!RoughlyZero((polygon[(i + 1) % len] - polygon[i]).Cross(LastAddedPoint() - polygon[i])))
                        points.Add(polygon[i]);

                // Return the new formed polygon
                return points.ToArray();
            }
        }

        public static IEnumerable<Double2[]> RemovePolygonWedges(Double2[] polygon, bool truncateDCEL = true)
        {
            // Quickly discard degenerate polygons
            if (polygon.Length < 3) return Enumerable.Empty<Double2[]>();

            // We will use a DCEL to do it
            var dcel = new PathCompiler.DCEL.DCEL(truncateDCEL);

            // Note the polygon winding
            double winding = 0;

            for (int i = 0; i < polygon.Length; i++)
            {
                var ik = (i + 1) % polygon.Length;
                dcel.AddCurve(PathCompiler.Curve.Line(polygon[i], polygon[ik]));
                winding += polygon[i].Cross(polygon[ik]);
            }

            // Now, remove the wedges
            dcel.RemoveWedges();

            // Now, ensure the windings are coherent with the original face's winding
            Double2[] ConstructPolygon(PathCompiler.DCEL.Face face)
            {
                var array = face.Edges.Select(p => p.Curve.A).ToArray();
                if (winding < 0) Array.Reverse(array);
                return array;
            }

            // And collect the faces
            return dcel.Faces.Where(f => !f.IsOuterFace).Select(ConstructPolygon);
        }

        public static Double2[] CircleIntersection(Double2 c1, double r1, Double2 c2, double r2)
        {
            // Firstly, rotate the second circle so it stands on the X-axis
            var rot = (c2 - c1).Normalized;

            // Get the displacement
            var a = (c2 - c1).Dot(rot);
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

        public static Double2[] CircleLineIntersection(Double2 c1, double r1, Double2 c, Double2 v)
        {
            // Firstly, rotate the second circle so it stands on the X-axis
            var rot = v.Normalized;

            // Get the displacement
            var y = -(c - c1).Cross(rot);
            var xs = r1 * r1 - y * y;

            // No intersection if this is negative
            if (xs < 0) return new Double2[0];
            else
            {
                var x = Math.Sqrt(xs);
                var pos = new Double2[] { new Double2(x, y), new Double2(-x, y) };

                // Project back to the new position
                return pos.Select(p => c1 + p.RotScale(rot)).ToArray();
            }
        }

        public static Double2[] ConvexHull(Double2[] points)
        {
            // Sort the points using the canonical comparer
            Array.Sort(points, CanonicalComparer.Default);
            ArrayExtensions.RemoveDuplicates(ref points);

            var hull = new List<Double2>(points.Length);
            // Work with the points array forwards and backwards
            for (int n = 0; n < 2; n++)
            {
                var hullPart = new List<Double2>(points.Length / 2);

                // Add the first two points
                hullPart.Add(points[0]);
                hullPart.Add(points[1]);

                // Run through the array
                for (int i = 2; i < points.Length; i++)
                {
                    // Rollback the possible vertices
                    while (hullPart.Count > 1 &&
                        (hullPart[hullPart.Count - 1] - hullPart[hullPart.Count - 2])
                        .Cross(points[i] - hullPart[hullPart.Count - 1]) > 0)
                        hullPart.RemoveAt(hullPart.Count - 1);

                    // Add the vertex
                    hullPart.Add(points[i]);
                }

                // Remove the last vertex
                hullPart.RemoveAt(hullPart.Count - 1);
                hull.AddRange(hullPart);
                Array.Reverse(points);
            }

            // Reverse the point orientation
            hull.Reverse();
            return hull.ToArray();
        }

        public static Double2[] EnsureCounterclockwise(Double2[] poly)
        {
            // Calculate the polygon's winding
            double winding = 0;

            for (int i = 0; i < poly.Length; i++)
                winding += poly[i].Cross(poly[(i + 1) % poly.Length]);

            // Reverse the polygon if the winding is clockwise
            if (winding < 0) poly.Reverse().ToArray();
            return poly;
        }
    }
}
