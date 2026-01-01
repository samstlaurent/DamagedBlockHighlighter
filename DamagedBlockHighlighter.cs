using HarmonyLib;
using System.Reflection;
using System.Collections;
using UnityEngine;

namespace DamagedBlockHighlighter
{
    public class DamagedBlockHighlighterInit : IModApi
    {
        public void InitMod(Mod _modInstance)
        {
            Log.Out("[DamagedBlockHighlighter] loaded successfully");

            // Ensure UI singleton exists
            var ui = DamagedBlockHighlighterUI.Instance;

            // Harmony patch example (if you have patches)
            var harmony = new Harmony("com.sams.damagedblockhighlighter");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
