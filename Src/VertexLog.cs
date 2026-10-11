using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace com.github.lhervier.ksp.diag.terrainquads
{
    /// <summary>
    /// The two CSV files the Log button of the window writes to, for this run of KSP. The first gets one line
    /// per Log: its number, the time of the game, the body, where the centre of the body is in the world,
    /// where the active vessel is and how fast it goes, how many times the origin of the world was shifted
    /// since the previous Log, and the reading of the seams as the window shows it (see
    /// <see cref="SeamReading"/>). The second gets one line per quad: the number of the Log, the name of
    /// the quad, its subdivision level, whether it shows, then the distance from the centre of the body to
    /// each of its vertices, in millimetres. Both are created at the first Log, in the PluginData folder of
    /// this mod; columns are separated by ";", and numbers written with "." as their decimal separator.
    /// </summary>
    internal static class VertexLog
    {
        private const string SEPARATOR = ";";

        private static readonly CultureInfo NUMBERS = CultureInfo.InvariantCulture;

        // The columns of the largest gap in the file of Logs, empty when there is none.
        private static readonly string[] LARGEST_COLUMNS =
        {
            "Finer quad at largest gap", "Step (mm)", "Beside (mm)", "Largest gap latitude",
            "Largest gap longitude", "Largest gap altitude (m)", "Largest gap under the sea",
            "Largest gap from vessel (km)", "Largest gap heading from vessel", "Finer quad", "Finer side",
            "Coarser quad", "Coarser side", "Coarser level"
        };

        // Static, so that the files and the numbering run on across scene changes, for the whole run of KSP.
        private static int lastLog;
        private static string logsPath;
        private static string quadsPath;

        /// <summary>The file of one line per Log; null before the first Log.</summary>
        public static string LogsPath => logsPath;

        /// <summary>The file of one line per quad; null before the first Log.</summary>
        public static string QuadsPath => quadsPath;

        /// <summary>
        /// Writes a Log of <paramref name="quads"/>, quads of the terrain of <paramref name="body"/>, and of
        /// <paramref name="reading"/>, the seams of that terrain, with <paramref name="shifts"/> as the number
        /// of shifts of the origin since the previous Log, and returns its number.
        /// </summary>
        public static int Write(CelestialBody body, SeamReading reading, ICollection<PQ> quads, int shifts)
        {
            if (logsPath == null)
            {
                CreateFiles();
            }
            int log = ++lastLog;

            StringBuilder line = new StringBuilder();
            Vector3d centre = body.position;
            Vessel vessel = FlightGlobals.ActiveVessel;
            line.Append(log).Append(SEPARATOR)
                .Append(Number(Planetarium.GetUniversalTime())).Append(SEPARATOR)
                .Append(body.bodyName).Append(SEPARATOR)
                .Append(Number(centre.x)).Append(SEPARATOR)
                .Append(Number(centre.y)).Append(SEPARATOR)
                .Append(Number(centre.z)).Append(SEPARATOR);
            if (vessel != null)
            {
                line.Append(vessel.vesselName.Replace(SEPARATOR, ",")).Append(SEPARATOR)
                    .Append(Number(vessel.latitude)).Append(SEPARATOR)
                    .Append(Number(vessel.longitude)).Append(SEPARATOR)
                    .Append(Number(vessel.altitude)).Append(SEPARATOR)
                    .Append(Number(vessel.srfSpeed)).Append(SEPARATOR)
                    .Append(Number(vessel.obt_speed)).Append(SEPARATOR);
            }
            else
            {
                // The six columns of the vessel, left empty.
                for (int i = 0; i < 6; i++)
                {
                    line.Append(SEPARATOR);
                }
            }
            line.Append(Number(Krakensbane.GetFrameVelocity().magnitude)).Append(SEPARATOR)
                .Append(shifts).Append(SEPARATOR)
                .Append(quads.Count);
            AppendReading(line, reading);
            File.AppendAllText(logsPath, line.Append('\n').ToString());

            StringBuilder rows = new StringBuilder();
            foreach (PQ quad in quads)
            {
                AppendQuad(rows, log, quad, centre);
            }
            File.AppendAllText(quadsPath, rows.ToString());
            return log;
        }

        /// <summary>Appends to <paramref name="line"/> the columns of <paramref name="reading"/>, each one
        /// after a separator, those of the largest gap left empty when there is none.</summary>
        private static void AppendReading(StringBuilder line, SeamReading reading)
        {
            line.Append(SEPARATOR).Append(reading.Level)
                .Append(SEPARATOR).Append(reading.Seams)
                .Append(SEPARATOR).Append(reading.FineQuads)
                .Append(SEPARATOR).Append(reading.CoarseQuads)
                .Append(SEPARATOR).Append(reading.Shared)
                .Append(SEPARATOR).Append(reading.NoCounterpart)
                .Append(SEPARATOR).Append(Number(reading.MeanGap * 1000.0))
                .Append(SEPARATOR).Append(Number(reading.MaxGap * 1000.0));
            if (!reading.HasLargest)
            {
                for (int i = 0; i < LARGEST_COLUMNS.Length; i++)
                {
                    line.Append(SEPARATOR);
                }
                return;
            }
            line.Append(SEPARATOR).Append(reading.Vertical >= 0.0 ? "above" : "below")
                .Append(SEPARATOR).Append(Number(System.Math.Abs(reading.Vertical) * 1000.0))
                .Append(SEPARATOR).Append(Number(reading.Horizontal * 1000.0))
                .Append(SEPARATOR).Append(Number(reading.Latitude))
                .Append(SEPARATOR).Append(Number(reading.Longitude))
                .Append(SEPARATOR).Append(Number(reading.Altitude))
                .Append(SEPARATOR).Append(reading.UnderSea ? "yes" : "no")
                .Append(SEPARATOR).Append(reading.HasVessel ? Number(reading.Distance / 1000.0) : "")
                .Append(SEPARATOR).Append(reading.HasVessel ? Number(reading.Heading) : "")
                .Append(SEPARATOR).Append(reading.FineQuad)
                .Append(SEPARATOR).Append(reading.FineSide)
                .Append(SEPARATOR).Append(reading.CoarseQuad)
                .Append(SEPARATOR).Append(reading.CoarseSide)
                .Append(SEPARATOR).Append(reading.CoarseLevel);
        }

        /// <summary>Appends to <paramref name="rows"/> the line of <paramref name="quad"/> in Log
        /// <paramref name="log"/>, its distances measured from <paramref name="centre"/>.</summary>
        private static void AppendQuad(StringBuilder rows, int log, PQ quad, Vector3d centre)
        {
            rows.Append(log).Append(SEPARATOR)
                .Append(quad.name).Append(SEPARATOR)
                .Append(quad.subdivision).Append(SEPARATOR)
                .Append(quad.isVisible ? "yes" : "no");

            // Each vertex taken through the matrix the quad is drawn with, in double, as the seams are: the
            // distance is the one from the centre of the body to the place the game puts the vertex.
            UnityEngine.Matrix4x4 matrix = quad.transform.localToWorldMatrix;
            UnityEngine.Vector3[] verts = quad.verts;
            for (int i = 0; i < verts.Length; i++)
            {
                double distance = (Seam.ToWorld(matrix, verts[i]) - centre).magnitude;
                rows.Append(SEPARATOR).Append((distance * 1000.0).ToString("F3", NUMBERS));
            }
            rows.Append('\n');
        }

        /// <summary>Creates both files, with their line of column names, named after the time they are
        /// created.</summary>
        private static void CreateFiles()
        {
            string folder = Path.Combine(Path.GetDirectoryName(typeof(VertexLog).Assembly.Location), "PluginData");
            Directory.CreateDirectory(folder);
            string stamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss", NUMBERS);

            logsPath = Path.Combine(folder, "logs-" + stamp + ".csv");
            List<string> columns = new List<string>
            {
                "Log", "UT", "Body", "Body x (m)", "Body y (m)", "Body z (m)", "Vessel", "Latitude", "Longitude",
                "Altitude (m)", "Surface speed (m/s)", "Orbital speed (m/s)", "Krakensbane (m/s)",
                "Origin shifts since previous Log", "Quads", "Level", "Seams", "Finer quads", "Coarser quads",
                "Shared vertices", "Vertices with no counterpart", "Gap mean (mm)", "Gap max (mm)"
            };
            columns.AddRange(LARGEST_COLUMNS);
            File.WriteAllText(logsPath, string.Join(SEPARATOR, columns) + "\n");

            StringBuilder header = new StringBuilder("Log;Quad;Level;Visible");
            for (int i = 0; i < PQS.cacheVertCount; i++)
            {
                header.Append(SEPARATOR).Append('v').Append(i).Append(" (mm)");
            }
            quadsPath = Path.Combine(folder, "quads-" + stamp + ".csv");
            File.WriteAllText(quadsPath, header.Append('\n').ToString());
        }

        private static string Number(double value)
        {
            return value.ToString("R", NUMBERS);
        }
    }
}
