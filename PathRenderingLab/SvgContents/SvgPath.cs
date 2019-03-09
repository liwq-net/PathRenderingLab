﻿using System.Collections.Generic;
using System.Xml;

namespace PathRenderingLab.SvgContents
{
    /// <summary>
    /// A class that represents an SVG path element
    /// </summary>
    public class SvgPath : SvgNode
    {
        /// <summary>
        /// The path data for this path
        /// </summary>
        public Path Path { get; private set; }

        /// <summary>
        /// The path length, used for dashing
        /// </summary>
        public double PathLength { get; private set; }

        public SvgPath(XmlNode child, SvgGroup parent) : base(child, parent)
        {

        }

        // Override the parse
        protected override void Parse(Dictionary<string, string> properties)
        {
            // Parse the common properties
            base.Parse(properties);

            // Parse the path data
            Path = new Path(properties.GetOrDefault("d", ""));

            // And the path length
            PathLength = DoubleUtils.TryParse(properties.GetOrDefault("pathLength")) ?? double.NaN;
        }
    }
}