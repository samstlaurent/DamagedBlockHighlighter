using System.IO;
using System.Xml;
using UnityEngine;

namespace DamagedBlockHighlighter
{
    // Holds config values only
    public class DamagedBlockHighlighterConfig
    {
        public float ScanInterval = 0.2f;
        public float ScanRange = 15f;
        public bool ScanOnlyWithRepairTool = true;

        public Color HighlightColor = new Color(1f, 0f, 1f, 0.5f);
        public float HighlightScale = 1.01f;
    }

    // Responsible for loading the config
    public static class ConfigLoader
    {
        public static DamagedBlockHighlighterConfig Load()
        {
            var config = new DamagedBlockHighlighterConfig();

            try
            {
                string dllPath = typeof(ConfigLoader).Assembly.Location;
                string modDir = Path.GetDirectoryName(dllPath);
                string path = Path.Combine(modDir, "config.xml");

                if (!File.Exists(path))
                {
                    Log.Warning("[DamagedBlockHighlighter] Config not found, using defaults");
                    return config;
                }

                var doc = new XmlDocument();
                doc.Load(path);
                var root = doc.DocumentElement;

                var scan = root["Scan"];
                config.ScanInterval = float.Parse(scan["Interval"].InnerText);
                config.ScanRange = float.Parse(scan["Range"].InnerText);
                config.ScanOnlyWithRepairTool =
                    bool.Parse(scan["OnlyWithRepairTool"].InnerText);

                var colorNode = root["Highlight"]["Color"];
                config.HighlightColor = new Color(
                    float.Parse(colorNode.Attributes["r"].Value),
                    float.Parse(colorNode.Attributes["g"].Value),
                    float.Parse(colorNode.Attributes["b"].Value),
                    float.Parse(colorNode.Attributes["a"].Value)
                );

                config.HighlightScale =
                    float.Parse(root["Highlight"]["Scale"].InnerText);
            }
            catch (System.Exception e)
            {
                Log.Error($"[DamagedBlockHighlighter] Config load failed: {e}");
            }

            return config;
        }
    }
}
