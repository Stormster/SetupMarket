﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigSection : ObservableCollection<IPythonAppConfigValue>, IWithId {
        public string Key { get; }

        public string DisplayName { get; }
        
        public string ToolTip { get; }
        
        public bool IsSingleSection { get; set; }

        private static readonly Regex SectionName = new Regex(@"^([^(]*)(?:\((.+)\))?", RegexOptions.Compiled);

        public string HintTop { get; }

        public string HintBottom { get; }

        private static string PrepareHint(string hint) {
            if (string.IsNullOrWhiteSpace(hint)) return null;
            hint = Regex.Replace(hint, @"<(?=[a-z/])", "[");
            hint = Regex.Replace(hint, @"(?<=[a-z""])>", "]");
            return hint;
        }

        private static bool DoNotShow(string configName, string sectionName, string key) {
            return key.StartsWith(@"__HINT") || key == @"CONTROLLER" && sectionName == @"BASIC" && configName.EndsWith(@"cfg\extension\weather_fx.ini");
        }

        public PythonAppConfigSection(string filename, [NotNull] PythonAppConfigParams configParams, KeyValuePair<string, IniFileSection> pair, 
                [CanBeNull] IniFileSection values)
                : base(pair.Value
                        .Where(x => !DoNotShow(filename, pair.Key, x.Key))
                        .Select(x => PythonAppConfigValue.Create(configParams, x,
                                pair.Value.Commentaries?.GetValueOrDefault(x.Key)?.Split('\n')[0],
                                values?.GetValueOrDefault(x.Key), values != null)).NonNull()) {
            Key = pair.Key;
            HintTop = PrepareHint(pair.Value.GetNonEmpty("__HINT_TOP"));
            HintBottom = PrepareHint(pair.Value.GetNonEmpty("__HINT_BOTTOM"));

            var commentary = pair.Value.Commentary?.Split('\n')[0].Trim();
            if (commentary == @"hidden") {
                DisplayName = @"hidden";
            } else {
                var name = commentary ?? PythonAppConfig.ConvertKeyToName(pair.Key);

                var match = SectionName.Match(name);
                if (match.Success && match.Groups[2].Success && match.Groups[2].Length > 50) {
                    name = PythonAppConfig.CapitalizeFirst(match.Groups[1].Value).Trim();
                    ToolTip = PythonAppConfig.CapitalizeFirst(match.Groups[2].Value).Trim();
                }

                DisplayName = PythonAppConfig.CapitalizeFirst(name);
            }
        }

        public string FullName => this.GetByIdOrDefault("FULLNAME")?.Value ?? DisplayName;
        public string Preview => this.GetByIdOrDefault("PREVIEW")?.Value;
        public string Order => this.GetByIdOrDefault("ORDER")?.Value;
        public string Description => this.GetByIdOrDefault("DESCRIPTION")?.Value;
        public string ShortDescription => this.GetByIdOrDefault("SHORT_DESCRIPTION")?.Value;

        public string Url {
            get {
                var url = this.GetByIdOrDefault("URL")?.Value;
                return string.IsNullOrWhiteSpace(url) ? null : $"[url={BbCodeBlock.EncodeAttribute(url)}]More info[/url].";
            }
        }

        public string Id => Key;
    }
}