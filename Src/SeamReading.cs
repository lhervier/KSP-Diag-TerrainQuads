using System;
using System.Collections.Generic;

namespace com.github.lhervier.ksp.diag.terrainquads
{
    /// <summary>
    /// What the seams of a body show at one refresh: how many there are, between how many quads, how far
    /// apart the two quads put the vertices they share, and where the largest gap is. The window shows it,
    /// KSP-Util-MCPServer reads it, and the Log button writes it to its file of Logs: the three always say the
    /// same thing.
    /// </summary>
    internal sealed class SeamReading
    {
        /// <summary>The body whose terrain was measured.</summary>
        public string Body { get; }

        /// <summary>The highest subdivision level of its terrain.</summary>
        public int Level { get; }

        /// <summary>Number of vertices along a side of a quad.</summary>
        public int SideVertices { get; }

        /// <summary>Number of seams: sides of a quad of the highest level against a coarser quad.</summary>
        public int Seams { get; }

        /// <summary>Number of quads of the highest level with at least one seam.</summary>
        public int FineQuads { get; }

        /// <summary>Number of coarser quads with at least one seam.</summary>
        public int CoarseQuads { get; }

        /// <summary>Number of vertices of a finer side matched with a vertex of the coarser side.</summary>
        public int Shared { get; }

        /// <summary>Number of vertices of a finer side with no vertex of the coarser side close enough.</summary>
        public int NoCounterpart { get; }

        /// <summary>Mean gap over the shared vertices, in metres; 0 when none is shared.</summary>
        public double MeanGap { get; }

        /// <summary>Largest gap over the shared vertices, in metres; 0 when none is shared.</summary>
        public double MaxGap { get; }

        /// <summary>Whether a largest gap was found; every member below is meaningless when not.</summary>
        public bool HasLargest { get; }

        /// <summary>The quad of the highest level at the largest gap.</summary>
        public string FineQuad { get; }

        /// <summary>The side of <see cref="FineQuad"/> along which the largest gap lies.</summary>
        public Side FineSide { get; }

        /// <summary>The coarser quad at the largest gap.</summary>
        public string CoarseQuad { get; }

        /// <summary>The side of <see cref="CoarseQuad"/> that faces <see cref="FineQuad"/>.</summary>
        public Side CoarseSide { get; }

        /// <summary>The subdivision level of <see cref="CoarseQuad"/>.</summary>
        public int CoarseLevel { get; }

        /// <summary>The largest gap along the vertical of the body at its vertex, in metres: positive when
        /// the finer quad is above the coarser one.</summary>
        public double Vertical { get; }

        /// <summary>The largest gap across the vertical of the body at its vertex, in metres.</summary>
        public double Horizontal { get; }

        /// <summary>Where the vertex of the largest gap is: latitude and longitude in degrees, height
        /// above sea level in metres.</summary>
        public double Latitude { get; }
        public double Longitude { get; }
        public double Altitude { get; }

        /// <summary>Whether the sea covers the vertex of the largest gap.</summary>
        public bool UnderSea { get; }

        /// <summary>Whether there is an active vessel, which <see cref="Distance"/> and
        /// <see cref="Heading"/> are measured from.</summary>
        public bool HasVessel { get; }

        /// <summary>From the active vessel to the vertex of the largest gap, in metres.</summary>
        public double Distance { get; }

        /// <summary>The bearing of that vertex from the active vessel, in degrees from north towards
        /// east.</summary>
        public double Heading { get; }

        /// <summary>
        /// Reads <paramref name="seams"/>, all measured, of the terrain <paramref name="sphere"/> of
        /// <paramref name="body"/>, between <paramref name="fineQuads"/> quads of the highest level and
        /// <paramref name="coarseQuads"/> coarser ones; <paramref name="worst"/> is the seam with the
        /// largest gap, or null.
        /// </summary>
        public SeamReading(CelestialBody body, PQS sphere, ICollection<Seam> seams, int fineQuads, int coarseQuads,
            Seam worst)
        {
            Body = body.bodyName;
            Level = sphere.maxLevel;
            SideVertices = QuadGrid.SideVertices;
            Seams = seams.Count;
            FineQuads = fineQuads;
            CoarseQuads = coarseQuads;

            double sum = 0.0;
            foreach (Seam seam in seams)
            {
                Shared += seam.Matched;
                NoCounterpart += seam.Unmatched;
                sum += seam.SumGap;
            }
            MeanGap = Shared > 0 ? sum / Shared : 0.0;
            MaxGap = worst != null ? worst.MaxGap : 0.0;
            if (worst == null)
            {
                return;
            }

            HasLargest = true;
            FineQuad = worst.Fine.name;
            FineSide = worst.FineSide;
            CoarseQuad = worst.Coarse.name;
            CoarseSide = worst.CoarseSide;
            CoarseLevel = worst.Coarse.subdivision;

            // Split along the vertical of the body at that vertex: a vertical gap is a step in the ground, a
            // horizontal one a crack or an overlap.
            Vector3d position = worst.WorstPosition;
            Vector3d up = (position - body.position).normalized;
            Vertical = Vector3d.Dot(worst.WorstGap, up);
            Horizontal = (worst.WorstGap - up * Vertical).magnitude;

            Latitude = body.GetLatitude(position);
            Longitude = body.GetLongitude(position);
            Altitude = body.GetAltitude(position);
            UnderSea = body.ocean && Altitude < 0.0;

            Vessel active = FlightGlobals.ActiveVessel;
            if (active != null)
            {
                HasVessel = true;
                Distance = (position - (Vector3d)active.transform.position).magnitude;
                Heading = Bearing(active.latitude, active.longitude, Latitude, Longitude);
            }
        }

        /// <summary>The reading as the window shows it, one line per string.</summary>
        public List<string> Lines()
        {
            List<string> lines = new List<string>
            {
                Body + ", highest level " + Level + ", " + SideVertices + " vertices along a side of a quad"
            };
            if (Seams == 0)
            {
                lines.Add("No seam: no quad of level " + Level + " is shown against a coarser one");
                return lines;
            }
            lines.Add(Seams + " seam(s) between " + FineQuads + " quad(s) of level " + Level + " and "
                + CoarseQuads + " coarser one(s), " + Shared + " shared vertices");
            string gaps = "Gap: mean " + Mm(MeanGap) + ", max " + Mm(MaxGap);
            if (NoCounterpart > 0)
            {
                gaps += "; " + NoCounterpart + " vertices with no counterpart";
            }
            lines.Add(gaps);
            if (!HasLargest)
            {
                return lines;
            }
            lines.Add("Largest gap: the finer quad " + Mm(Math.Abs(Vertical)) + (Vertical >= 0.0 ? " above" : " below")
                + " the coarser one, " + Mm(Horizontal) + " beside it");
            string where = "At " + Latitude.ToString("F4") + "° lat, " + Longitude.ToString("F4") + "° lon, "
                + (UnderSea ? "under the sea" : Altitude.ToString("F0") + " m above sea level");
            if (HasVessel)
            {
                where += "; " + (Distance / 1000.0).ToString("F1") + " km from the vessel, heading "
                    + Heading.ToString("F0") + "°";
            }
            lines.Add(where);
            lines.Add("On " + FineQuad + " (" + FineSide + ", level " + Level + ") against " + CoarseQuad + " ("
                + CoarseSide + ", level " + CoarseLevel + ")");
            return lines;
        }

        /// <summary>The reading ready to be written as JSON, with the same members as the window
        /// shows.</summary>
        public Dictionary<string, object> ToJson()
        {
            Dictionary<string, object> json = new Dictionary<string, object>
            {
                { "body", Body },
                { "level", Level },
                { "sideVertices", SideVertices },
                { "seams", Seams },
                { "fineQuads", FineQuads },
                { "coarseQuads", CoarseQuads },
                { "sharedVertices", Shared },
                { "noCounterpart", NoCounterpart },
                { "meanGapMm", MeanGap * 1000.0 },
                { "maxGapMm", MaxGap * 1000.0 }
            };
            if (!HasLargest)
            {
                return json;
            }
            Dictionary<string, object> largest = new Dictionary<string, object>
            {
                { "gapMm", MaxGap * 1000.0 },
                { "verticalMm", Math.Abs(Vertical) * 1000.0 },
                { "finerAbove", Vertical >= 0.0 },
                { "horizontalMm", Horizontal * 1000.0 },
                { "latitude", Latitude },
                { "longitude", Longitude },
                { "altitude", Altitude },
                { "underSea", UnderSea },
                { "fineQuad", FineQuad },
                { "fineSide", FineSide.ToString() },
                { "coarseQuad", CoarseQuad },
                { "coarseSide", CoarseSide.ToString() },
                { "coarseLevel", CoarseLevel }
            };
            if (HasVessel)
            {
                largest["distanceKm"] = Distance / 1000.0;
                largest["headingFromVessel"] = Heading;
            }
            json["largest"] = largest;
            return json;
        }

        private static string Mm(double metres)
        {
            return (metres * 1000.0).ToString("F1") + " mm";
        }

        /// <summary>The initial bearing, in degrees from north towards east, of the great circle from one
        /// point to another.</summary>
        private static double Bearing(double lat1, double lon1, double lat2, double lon2)
        {
            double f1 = lat1 * Math.PI / 180.0;
            double f2 = lat2 * Math.PI / 180.0;
            double dl = (lon2 - lon1) * Math.PI / 180.0;
            double y = Math.Sin(dl) * Math.Cos(f2);
            double x = Math.Cos(f1) * Math.Sin(f2) - Math.Sin(f1) * Math.Cos(f2) * Math.Cos(dl);
            return (Math.Atan2(y, x) * 180.0 / Math.PI + 360.0) % 360.0;
        }
    }
}
