using System.IO;
using System.Xml;
using UnityEngine;

namespace DamagedBlockHighlighter
{
    public class DamagedBlockHighlighterConfig
    {
        public float ScanInterval = 0.2f; // seconds
        public float ScanRange = 15f; // meters
        public bool ScanOnlyWithRepairTool = true;
        public bool ScanIgnoreTerrain = false;
        public float ScanDamageThreshold = 0f; // proportion of damage (0-1)

        public Color HighlightColor = new Color(1f, 0f, 1f, 0.3f);
    }

    public static class ConfigLoader
    {
        public static DamagedBlockHighlighterConfig Load()
        {
            var config = new DamagedBlockHighlighterConfig();
            string dllPath = typeof(ConfigLoader).Assembly.Location;
            string modDir = Path.GetDirectoryName(dllPath);
            string path = Path.Combine(modDir, "config.xml");

            if (!File.Exists(path)) return config;

            var doc = new XmlDocument();
            doc.Load(path);
            var root = doc.DocumentElement;
            var scan = root["Scan"];
            var colorNode = root["Highlight"]["Color"];

            config.ScanInterval = float.Parse(scan["Interval"].InnerText);
            config.ScanRange = float.Parse(scan["Range"].InnerText);
            config.ScanOnlyWithRepairTool = bool.Parse(scan["OnlyWithRepairTool"].InnerText);
            config.ScanIgnoreTerrain = bool.Parse(scan["IgnoreTerrain"].InnerText);
            config.ScanDamageThreshold = float.Parse(scan["DamageThreshold"].InnerText);
            config.HighlightColor = new Color(
                float.Parse(colorNode.Attributes["r"].Value),
                float.Parse(colorNode.Attributes["g"].Value),
                float.Parse(colorNode.Attributes["b"].Value),
                float.Parse(colorNode.Attributes["a"].Value)
            );

            return config;
        }
    }
}
