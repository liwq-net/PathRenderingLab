﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PathRenderingLab
{
#if WINDOWS || LINUX
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        public struct PathDetails
        {
            public Path Path;

            public Color? FillColor;
            public Color? StrokeColor;
            public Color BackgroundColor;

            public FillRule FillRule;
            public double StrokeWidth;
            public StrokeLineCap StrokeLineCap;
            public StrokeLineJoin StrokeLineJoin;
            public double MiterLimit;
            public bool InvertY;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            string file;
            if (args.Length > 0) file = args[0];
            else
            {
                Console.Write("Enter path of file containing path details: ");
                file = Console.ReadLine();
            }

            var str = File.ReadAllText(file);
            var pd = GetPathDetailsFromString(str);

            Console.WriteLine($"Parsed path: {pd.Path}");
            Console.WriteLine();

            var fillTriangleIndices = new List<int>();
            var strokeTriangleIndices = new List<int>();
            var fillCurveVertices = new List<VertexPositionCurve>();
            var strokeCurveVertices = new List<VertexPositionCurve>();
            var vertexCache = new Dictionary<Vector2, int>();

            int IdForVertex(Vector2 v)
            {
                if (!vertexCache.ContainsKey(v))
                    vertexCache[v] = vertexCache.Count;
                return vertexCache[v];
            }

            var compiledFill = pd.FillColor.HasValue ? PathCompiler.CompileFill(pd.Path, pd.FillRule) : CompiledDrawing.Empty;
            var compiledStroke = pd.StrokeColor.HasValue ? PathCompiler.CompileStroke(pd.Path, pd.StrokeWidth, pd.StrokeLineCap,
                pd.StrokeLineJoin, pd.MiterLimit) : CompiledDrawing.Empty;

            foreach (var tri in compiledFill.Triangles)
            {
                fillTriangleIndices.Add(IdForVertex((Vector2)tri.A));
                fillTriangleIndices.Add(IdForVertex((Vector2)tri.B));
                fillTriangleIndices.Add(IdForVertex((Vector2)tri.C));
            }

            foreach (var tri in compiledFill.CurveTriangles)
            {
                fillCurveVertices.AddRange(new[]
                {
                    new VertexPositionCurve(new Vector3((Vector2)tri.A.Position, 0), (Vector4)tri.A.CurveCoords),
                    new VertexPositionCurve(new Vector3((Vector2)tri.B.Position, 0), (Vector4)tri.B.CurveCoords),
                    new VertexPositionCurve(new Vector3((Vector2)tri.C.Position, 0), (Vector4)tri.C.CurveCoords)
                });
            }

            int length = vertexCache.Count == 0 ? 0 : vertexCache.Max(p => p.Value) + 1;
            var fillVertices = new Vector2[length];
            foreach (var kvp in vertexCache) fillVertices[kvp.Value] = kvp.Key;

            vertexCache.Clear();

            foreach (var tri in compiledStroke.Triangles)
            {
                strokeTriangleIndices.Add(IdForVertex((Vector2)tri.A));
                strokeTriangleIndices.Add(IdForVertex((Vector2)tri.B));
                strokeTriangleIndices.Add(IdForVertex((Vector2)tri.C));
            }

            foreach (var tri in compiledStroke.CurveTriangles)
            {
                strokeCurveVertices.AddRange(new[]
                {
                    new VertexPositionCurve(new Vector3((Vector2)tri.A.Position, 0), (Vector4)tri.A.CurveCoords),
                    new VertexPositionCurve(new Vector3((Vector2)tri.B.Position, 0), (Vector4)tri.B.CurveCoords),
                    new VertexPositionCurve(new Vector3((Vector2)tri.C.Position, 0), (Vector4)tri.C.CurveCoords)
                });
            }

            length = vertexCache.Count == 0 ? 0 : vertexCache.Max(p => p.Value) + 1;
            var strokeVertices = new Vector2[length];
            foreach (var kvp in vertexCache) strokeVertices[kvp.Value] = kvp.Key;

            Console.WriteLine("Statistics:");
            Console.WriteLine($"{fillTriangleIndices.Count / 3 + fillCurveVertices.Count / 3} fill triangles, " +
                $"{fillTriangleIndices.Count / 3} full and {fillCurveVertices.Count / 3} curve.");
            Console.WriteLine($"{strokeTriangleIndices.Count / 3 + strokeCurveVertices.Count / 3} stroke triangles, " +
                $"{strokeTriangleIndices.Count / 3} full and {strokeCurveVertices.Count / 3} curve.");

            using (var game = new PathRenderingLab())
            {
                game.BackgroundColor = pd.BackgroundColor;
                game.FillColor = pd.FillColor ?? Color.Transparent;
                game.FillVertices = fillVertices;
                game.FillIndices = fillTriangleIndices.ToArray();
                game.FillCurveVertices = fillCurveVertices.ToArray();
                game.StrokeColor = pd.StrokeColor ?? Color.Transparent;
                game.StrokeVertices = strokeVertices;
                game.StrokeIndices = strokeTriangleIndices.ToArray();
                game.StrokeCurveVertices = strokeCurveVertices.ToArray();
                game.StrokeHalfWidth = (float)pd.StrokeWidth / 2;
                game.InvertY = pd.InvertY;
                game.PathString = str;

                game.Run();
            }
        }

        public static T? NullIfThrow<T>(Func<T> f) where T : struct
        {
            try { return new T?(f()); }
            catch { return new T?(); }
        }

        public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            if (dict.ContainsKey(key)) return dict[key];
            return defaultValue;
        }

        private static PathDetails GetPathDetailsFromString(string str)
        {
            // It must have exactly two lines
            var lines = str.Split('\n', '\r').Where(s => s.Length > 0).ToArray();
            if (lines.Length != 2) throw new InvalidOperationException("The path details file must have two non-blank lines!");

            // The first line is the path
            var path = new Path(lines[0]);

            // The second line is a list of options
            var opts = lines[1].Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).Select(s => s.Split(':')).ToArray();
            // All options must be... options
            if (!opts.All(l => l.Length == 2)) throw new InvalidOperationException("Ill-formed option list");

            // Concatenate all of them in a dictionary
            var optsDict = new Dictionary<string, string>();
            foreach (var opt in opts) optsDict.Add(opt[0].Trim(), opt[1].Trim());

            // And pick the options
            var fillColor = optsDict.GetOrDefault("fill", "none").ToLowerInvariant();
            var strokeColor = optsDict.GetOrDefault("stroke", "black").ToLowerInvariant();
            var backgroundColor = optsDict.GetOrDefault("background", "white").ToLowerInvariant();
            var fillRule = optsDict.GetOrDefault("fill-rule", "nonzero").ToLowerInvariant();

            var strokeWidth = optsDict.GetOrDefault("stroke-width", "0");
            var strokeLineCap = optsDict.GetOrDefault("stroke-linecap", "butt").ToLowerInvariant();
            var strokeLineJoin = optsDict.GetOrDefault("stroke-linejoin", "bevel").ToLowerInvariant();
            var miterLimit = optsDict.GetOrDefault("stroke-miterlimit", "Infinity");
            var invertY = optsDict.GetOrDefault("invert-y", "false").ToLowerInvariant();

            double? GetDouble(string s) => NullIfThrow(() => double.Parse(s, CultureInfo.InvariantCulture));

            return new PathDetails()
            {
                Path = path,

                FillColor = CSSColor.Parse(fillColor),
                StrokeColor = CSSColor.Parse(strokeColor),
                BackgroundColor = CSSColor.Parse(backgroundColor) ?? Color.White,

                FillRule = CSSEnumPicker<FillRule>.Get(fillRule) ?? FillRule.Nonzero,
                StrokeWidth = strokeColor == "none" ? 0 : GetDouble(strokeWidth) ?? 0,
                StrokeLineCap = CSSEnumPicker<StrokeLineCap>.Get(strokeLineCap) ?? StrokeLineCap.Butt,
                StrokeLineJoin = CSSEnumPicker<StrokeLineJoin>.Get(strokeLineJoin) ?? StrokeLineJoin.Bevel,
                MiterLimit = GetDouble(miterLimit) ?? double.PositiveInfinity,

                InvertY = invertY == "true" || invertY == "yes" || invertY == "y",
            };
        }
    }
#endif
}