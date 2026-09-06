using System;
using System.IO;
using System.Text.Json;

namespace KnotJotUiEditor
{
    public sealed class UiSkinProtocolDefinition
    {
        public string Format { get; set; } = "knotjot-ui-skins";
        public int Version { get; set; } = 1;
        public string LocationFormat { get; set; } = "knotjot-skins-location";
        public string ProjectApp { get; set; } = "knotjot-ui-editor-project";
        public string ProjectSchema { get; set; } = "knotjot-ui-skin";
        public int ProjectSchemaVersion { get; set; } = 2;
        public string IdPattern { get; set; } = "^[A-Za-z0-9_-]{3,80}$";
        public string FileName { get; set; } = "ui-skins.json";
        public string ApplicationDataDirectory { get; set; } = "KnotJot";
        public string LocationFileName { get; set; } = "skin-sync-location.json";
        public UiSkinProtocolLimits Limits { get; set; } = new UiSkinProtocolLimits();
    }

    public sealed class UiSkinProtocolLimits
    {
        public int LibraryBytes { get; set; } = 16 * 1024 * 1024;
        public int CssBytes { get; set; } = 2 * 1024 * 1024;
        public int SkinCount { get; set; } = 500;
        public int NameCharacters { get; set; } = 60;
        public int IdCharacters { get; set; } = 80;
    }

    public static class UiSkinProtocol
    {
        public static UiSkinProtocolDefinition Current { get; } = Load();

        // Load the packaged protocol definition and retain built-in defaults if it is unavailable.
        // 加载随程序发布的协议定义，不可用时保留内置默认值。
        private static UiSkinProtocolDefinition Load()
        {
            try
            {
                string file = Path.Combine(AppContext.BaseDirectory, "protocols", "ui-skin-protocol.json");
                if (!File.Exists(file)) return new UiSkinProtocolDefinition();
                return JsonSerializer.Deserialize<UiSkinProtocolDefinition>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new UiSkinProtocolDefinition();
            }
            catch { return new UiSkinProtocolDefinition(); }
        }
    }
}
