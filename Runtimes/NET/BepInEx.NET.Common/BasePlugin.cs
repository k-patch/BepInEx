using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BepInEx.NET.Common
{
    public abstract class BasePlugin
    {
        protected BasePlugin()
        {
            var metadata = MetadataHelper.GetMetadata(this);

            HarmonyInstance = new Harmony("BepInEx.Plugin." + metadata.GUID);
            HarmonyInstance.PatchAll(this.GetType().Assembly);

            Log = Logger.CreateLogSource(metadata.Name);

            Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, metadata.GUID + ".cfg"), false, metadata);
        }

        public ManualLogSource Log { get; }

        public ConfigFile Config { get; }

        public Harmony HarmonyInstance { get; set; }

        public PluginInfo Info { get; internal set; }

        public string Directory => System.IO.Path.GetDirectoryName(Info.Location);

        public abstract void Load();

        public virtual bool Unload() => false;
    }

    public abstract class BasePlugin<T> : BasePlugin where T : BasePlugin<T>
    {
        public static T Instance { get; private set; }
        public static new ManualLogSource Log => ((BasePlugin)Instance).Log;

        protected BasePlugin() : base()
        {
            Instance = (T)this;
        }
    }
}
