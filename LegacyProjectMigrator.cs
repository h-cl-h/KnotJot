using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace KnotJotUiEditor
{
    public sealed class ProjectMigrationResult
    {
        public Dictionary<string, string> Designs { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<string> Warnings { get; } = new List<string>();
        public string ActiveTarget { get; set; } = "card";
        public string SourceVersion { get; set; } = "V1.0.0";
        public bool Migrated { get; set; }
    }

    /// <summary>纯数据迁移：识别 V0.0.1/V0.0.2/V0.0.3/V1.0.0，不直接修改窗口状态。</summary>
    public static class LegacyProjectMigrator
    {
        // Route supported project schemas into current per-target design envelopes and report migration notes.
        // 将受支持的工程结构转换为当前的分目标设计数据，并报告迁移说明。
        public static ProjectMigrationResult Read(JsonElement root, JsonSerializerOptions options)
        {
            var result = new ProjectMigrationResult();
            result.ActiveTarget = String(root, "activeTarget") ?? "card";
            if (root.TryGetProperty("designs", out var designs) && designs.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in designs.EnumerateObject())
                    if (DesignTargetLib.Find(p.Name) != null) result.Designs[p.Name] = NormalizeDesignEnvelope(p.Value, options);
                int version = Int(root, "version");
                result.SourceVersion = version >= 4 ? "V1.0.0" : "V0.0.3";
                result.Migrated = version < 5 || String(root, "schema") != UiSkinProtocol.Current.ProjectSchema;
                if (result.Designs.Count == 0) result.Warnings.Add("文件没有可识别的设计目标。");
                return result;
            }

            if (!root.TryGetProperty("project", out var project) || project.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("工程既没有 designs，也没有旧版 project 数据。");

            if (project.TryGetProperty("regions", out var regions) && regions.ValueKind == JsonValueKind.Object)
            {
                result.SourceVersion = "V0.0.1"; result.Migrated = true;
                MigrateRegions(regions, result, options);
                if (project.TryGetProperty("shapes", out var shapes) && shapes.ValueKind == JsonValueKind.Array)
                    AppendRawElements(result, "card", shapes.EnumerateArray().Select(/* Clone JSON data beyond its document lifetime. 克隆 JSON 数据以脱离文档生命周期。 */ x => x.Clone()), options);
                result.Warnings.Add("V0.0.1 的整页坐标已并入“普通节点”设计；请在另存为新格式前检查位置。");
                return result;
            }

            if (project.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
            {
                result.SourceVersion = "V0.0.2"; result.Migrated = true;
                MigrateElements(elements, result, options);
                result.Warnings.Add("V0.0.2 的整页装饰已按部件拆分；未能识别的装饰保留在“普通节点”设计中。");
                return result;
            }
            throw new InvalidOperationException("无法识别旧工程 schema（需要 regions+shapes、elements 或 designs）。");
        }

        // Keep valid element envelopes and replace unsupported target payloads with empty designs.
        // 保留有效元素封装，将不支持的目标数据替换为空设计。
        private static string NormalizeDesignEnvelope(JsonElement design, JsonSerializerOptions options)
        {
            if (design.ValueKind != JsonValueKind.Object) return JsonSerializer.Serialize(new { schemaVersion = 4, elements = Array.Empty<object>() }, options);
            if (design.TryGetProperty("elements", out _)) return design.GetRawText();
            return JsonSerializer.Serialize(new { schemaVersion = 4, elements = Array.Empty<object>() }, options);
        }

        // Translate the oldest region-color format into editable node and text design elements.
        // 将最早的区域颜色格式转换为可编辑节点与文本设计元素。
        private static void MigrateRegions(JsonElement regions, ProjectMigrationResult result, JsonSerializerOptions options)
        {
            // Read a legacy ordinary-card color with the supplied default.
            // 读取旧版普通卡片颜色，并使用指定默认值兜底。
            string Card(string key, string fallback) => RegionColor(regions, "card", key, fallback);
            // Read a legacy selected-card color with the supplied default.
            // 读取旧版选中卡片颜色，并使用指定默认值兜底。
            string Sel(string key, string fallback) => RegionColor(regions, "sel", key, fallback);
            // Read a legacy text-region color with the supplied default.
            // 读取旧版文本区域颜色，并使用指定默认值兜底。
            string Text(string key, string fallback) => RegionColor(regions, "text", key, fallback);
            AddVisual(result, "card", Card("bg", "#ffffff"), Card("border", "#e2e5ec"), Card("text", "#23262e"), options);
            AddVisual(result, "cardSel", Card("bg", "#ffffff"), Sel("accent", "#5b8def"), Card("text", "#23262e"), options);
            AddVisual(result, "toolbar", RegionColor(regions, "toolbar", "bg", "#ffffff"), RegionColor(regions, "toolbar", "border", "#e2e5ec"), null, options);
            AddVisual(result, "title", null, null, Text("ink", "#23262e"), options);
            AddVisual(result, "dot", Sel("accent", "#5b8def"), null, null, options);
            AddVisual(result, "btn", RegionColor(regions, "btn", "bg", "#ffffff"), RegionColor(regions, "btn", "border", "#e2e5ec"), RegionColor(regions, "btn", "text", "#23262e"), options);
            AddVisual(result, "btnPrimary", RegionColor(regions, "btnPrimary", "bg", "#5b8def"), null, RegionColor(regions, "btnPrimary", "text", "#ffffff"), options);
            AddVisual(result, "fname", null, null, Text("muted", "#8a90a0"), options);
        }

        // Convert legacy component properties into target designs and preserve remaining raw elements.
        // 将旧版部件属性转换为目标设计，并保留其余原始元素。
        private static void MigrateElements(JsonElement elements, ProjectMigrationResult result, JsonSerializerOptions options)
        {
            var leftovers = new List<JsonElement>();
            foreach (var element in elements.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object || String(element, "type") != "component") { leftovers.Add(element.Clone()); continue; }
                string oldId = String(element, "comp");
                string id = oldId == "menu" ? "menuBox" : oldId;
                if (DesignTargetLib.Find(id) == null) { leftovers.Add(element.Clone()); continue; }
                var props = element.TryGetProperty("props", out var p) && p.ValueKind == JsonValueKind.Object ? p : default;
                string fill = PropColor(props, "bg") ?? PropColor(props, "color");
                string stroke = PropColor(props, "border") ?? PropColor(props, "accent");
                string text = PropColor(props, "text") ?? PropColor(props, "ink");
                AddVisual(result, id, fill, stroke, text, options);
            }
            if (leftovers.Count > 0) AppendRawElements(result, "card", leftovers, options);
        }

        // Create a target-sized background shape and optional text region from migrated colors.
        // 根据迁移颜色创建匹配目标尺寸的背景图形及可选文本区域。
        private static void AddVisual(ProjectMigrationResult result, string targetId, string fill, string stroke, string textColor, JsonSerializerOptions options)
        {
            var target = DesignTargetLib.Find(targetId);
            if (target == null) return;
            var elements = new List<object>();
            Rect f = target.FrameRect;
            if (!string.IsNullOrEmpty(fill) || !string.IsNullOrEmpty(stroke))
            {
                elements.Add(new
                {
                    type = "roundrect", id = "el_" + Guid.NewGuid().ToString("N"), name = "迁移的外观", visible = true,
                    x = f.X, y = f.Y, w = f.Width, h = f.Height, fill = fill ?? "#ffffff", noFill = string.IsNullOrEmpty(fill),
                    stroke = stroke ?? fill ?? "#ffffff", strokeW = string.IsNullOrEmpty(stroke) ? 0 : 1, radius = Math.Min(12, Math.Min(f.Width, f.Height) / 4), opacity = 1
                });
            }
            if (target.HasText && !string.IsNullOrEmpty(textColor))
            {
                Rect tr = target.DefaultTextRect;
                elements.Add(new
                {
                    type = "textregion", id = "el_" + Guid.NewGuid().ToString("N"), name = "文本区", visible = true,
                    x = tr.X, y = tr.Y, w = tr.Width, h = tr.Height, text = "文字", customText = false,
                    fontSize = 14, color = textColor, alignH = targetId == "card" || targetId == "cardSel" ? "left" : "center", alignV = "middle", bold = false
                });
            }
            result.Designs[targetId] = JsonSerializer.Serialize(new { schemaVersion = 4, elements }, options);
        }

        // Merge preserved legacy elements into the target's current serialized element list.
        // 将保留的旧元素合并到目标当前的序列化元素列表。
        private static void AppendRawElements(ProjectMigrationResult result, string targetId, IEnumerable<JsonElement> additions, JsonSerializerOptions options)
        {
            var all = new List<JsonElement>();
            if (result.Designs.TryGetValue(targetId, out var existing))
            {
                using var doc = JsonDocument.Parse(existing);
                if (doc.RootElement.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
                    all.AddRange(elements.EnumerateArray().Select(/* Clone JSON data beyond its document lifetime. 克隆 JSON 数据以脱离文档生命周期。 */ x => x.Clone()));
            }
            all.AddRange(additions);
            result.Designs[targetId] = JsonSerializer.Serialize(new { schemaVersion = 4, elements = all }, options);
        }

        // Read and normalize a named color from a legacy region, preserving the fallback on invalid data.
        // 从旧区域读取并规范指定颜色，无效数据使用回退值。
        private static string RegionColor(JsonElement regions, string region, string key, string fallback)
        {
            if (regions.TryGetProperty(region, out var r) && r.ValueKind == JsonValueKind.Object && r.TryGetProperty(key, out var v))
                return ColorUtil.NormalizeHex(v.GetString()) ?? fallback;
            return fallback;
        }

        // Read and normalize a color property from an optional legacy property object.
        // 从可选的旧属性对象读取并规范颜色属性。
        private static string PropColor(JsonElement props, string key)
        {
            if (props.ValueKind == JsonValueKind.Object && props.TryGetProperty(key, out var value)) return ColorUtil.NormalizeHex(value.GetString());
            return null;
        }

        // Read a JSON string only when both the owner and property types match.
        // 仅在所属对象与属性类型匹配时读取 JSON 字符串。
        private static string String(JsonElement owner, string name)
        {
            return owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        // Read an integer property safely, using zero for missing or incompatible values.
        // 安全读取整数属性，缺失或类型不兼容时使用零。
        private static int Int(JsonElement owner, string name)
        {
            return owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;
        }
    }
}
