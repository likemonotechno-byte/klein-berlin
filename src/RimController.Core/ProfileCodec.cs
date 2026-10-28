using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace RimController.Core
{
    // Embedded as a string in ModSettings so the game owns the settings file lifecycle.
    public static class ProfileCodec
    {
        public static string Encode(ControllerProfiles profiles)
        {
            var root = new XElement("profiles", new XAttribute("version", 1),
                new XAttribute("tutorialEnabled", profiles.TutorialEnabled));
            foreach (var profile in new[] { profiles.DualSense, profiles.DualShock4 })
                root.Add(new XElement("controller", new XAttribute("model", profile.Model),
                    profile.Export().Select(b => new XElement("binding", new XAttribute("context", b.Context),
                        new XAttribute("action", b.Action), new XAttribute("button", b.Button)))));
            root.Add(new XElement("lessons", profiles.Lessons.OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new XElement("lesson", new XAttribute("id", x.Key), new XAttribute("status", x.Value)))));
            return root.ToString(SaveOptions.DisableFormatting);
        }
        public static ControllerProfiles Decode(string? xml)
        {
            var result = new ControllerProfiles();
            if (string.IsNullOrWhiteSpace(xml)) return result; // Existing settings have no profile field.
            try
            {
                using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
                    { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 });
                var root = XElement.Load(reader);
                if (root.Name != "profiles" || (string?)root.Attribute("version") != "1")
                { result.LoadIssues.Add("RC_ProfileVersion"); return result; }
                if (bool.TryParse((string?)root.Attribute("tutorialEnabled"), out bool enabled)) result.TutorialEnabled = enabled;
                var seen = new HashSet<PadModel>();
                foreach (var node in root.Elements("controller"))
                {
                    if (!TryEnum(node, "model", out PadModel model) || result.For(model) == null || !seen.Add(model))
                    { result.LoadIssues.Add("RC_ProfileUnknown"); continue; }
                    var entries = new List<BindingOverride>();
                    foreach (var binding in node.Elements("binding"))
                    {
                        if (entries.Count >= 256) { result.LoadIssues.Add("RC_ProfileInvalid"); break; }
                        if (!TryEnum(binding, "context", out InputContext context) ||
                            !TryEnum(binding, "action", out ControllerAction action) || !TryEnum(binding, "button", out PadButton button))
                        { result.LoadIssues.Add("RC_ProfileUnknown"); continue; }
                        entries.Add(new BindingOverride(context, action, button));
                    }
                    result.For(model)!.Restore(entries);
                }
                var lessons = new Dictionary<string, LessonStatus>(StringComparer.Ordinal);
                foreach (var node in root.Element("lessons")?.Elements("lesson") ?? Enumerable.Empty<XElement>())
                {
                    string? id = (string?)node.Attribute("id");
                    if (string.IsNullOrWhiteSpace(id) || id!.Length > 256 || lessons.ContainsKey(id) ||
                        !TryEnum(node, "status", out LessonStatus status))
                    { result.LoadIssues.Add("RC_ProfileInvalid"); continue; }
                    lessons[id] = status;
                }
                result.RestoreLessons(lessons);
            }
            catch (XmlException)
            { result = new ControllerProfiles(); result.LoadIssues.Add("RC_ProfileInvalid"); }
            return result;
        }
        private static bool TryEnum<T>(XElement node, string attribute, out T value) where T : struct
            => Enum.TryParse((string?)node.Attribute(attribute), out value) && Enum.IsDefined(typeof(T), value);
    }
}
