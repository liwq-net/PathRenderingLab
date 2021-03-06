﻿using Bitlush;

namespace PathRenderingLab.PathCompiler.DCEL
{
    // Stored vertex data: outgoing vertices stored by angle order (counter-clockwise order)
    public class Vertex
    {
        public int ClusterIndex;
        public AvlTree<OuterAngles, Edge> OutgoingEdges;

        public Vertex(int id)
        {
            ClusterIndex = id;
            OutgoingEdges = new AvlTree<OuterAngles, Edge>();
        }

        public bool SearchOutgoingEdges(Edge e, out Edge eli, out Edge eri)
        {
            if (OutgoingEdges.SearchLeftRight(e.OuterAngles, out eli, out eri))
                return true;

            // Try to mimic a cyclical edge list
            if (eli == null) eli = OutgoingEdges.Last;
            if (eri == null) eri = OutgoingEdges.First;

            return false;
        }
    }
}