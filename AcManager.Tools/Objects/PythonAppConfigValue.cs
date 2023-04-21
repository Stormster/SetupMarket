﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using StringBasedFilter;
using StringBasedFilter.Parsing;
using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigValue : Displayable, IPythonAppConfigValue {
        public string OriginalKey { get; private set; }

        string IWithId<string>.Id => OriginalKey;

        [CanBeNull]
        public Func<IPythonAppConfigValueProvider, bool> IsEnabledTest { get; private set; }

        [CanBeNull]
        public Func<IPythonAppConfigValueProvider, bool> IsHiddenTest { get; private set; }

        [CanBeNull]
        public string ToolTip { get; private set; }

        public sealed override string DisplayName { get; set; }

        private string _value;

        [CanBeNull]
        public string Value {
            get => _value;
            set {
                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsChanged));
                OnPropertyChanged(nameof(IsNonDefault));
                OnValueChanged();
            }
        }

        public virtual string DisplayValueString => Value;

        protected virtual void OnValueChanged() { }

        private bool _isEnabled = true;

        public bool IsEnabled {
            get => _isEnabled;
            set => Apply(value, ref _isEnabled);
        }

        private bool _isHidden;

        public bool IsHidden {
            get => _isHidden;
            set => Apply(value, ref _isHidden);
        }

        private bool _isNew;

        public bool IsNew {
            get => _isNew;
            set => Apply(value, ref _isNew);
        }

        public string FilesRelativeDirectory { get; private set; }

        private static readonly Regex ValueCommentaryRegex = new Regex(
                @"^([^(;]*)(?:\(([^)]+)\))?(?:;(.*))?",
                RegexOptions.Compiled);

        private static readonly Regex RangeRegex = new Regex(
                @"^from\s+(-?\d+(?:\.\d*)?|[_a-zA-Z]\w+)( ?\S+)?\s+to\s+(-?\d+(?:\.\d*)?|[_a-zA-Z]\w+)(,\s*per)?(?:,\s*round\s+to\s+(\d+(?:\.\d*)?))?(,\s*round)?",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex NumberRegex = new Regex(
                @"^(?:number|float)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex KeyRegex = new Regex(
                @"^(?:key|button|keyboard|keyboard button)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BooleanRegex = new Regex(
                @"^([\w-]+)\s+or\s+([\w-]+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OptionsRegex = new Regex(
                @"(?:^|\s*,|\s*\bor\b)\s*(?:[""`'“”](.+?)[""`'“”]|(((?!\bor\b)[^,])+))",
                RegexOptions.Compiled);

        private static readonly Regex OptionValueRegex = new Regex(
                @"^(.+)(?:\s+is\s+|=)(.+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OptionValueAltRegex = new Regex(
                @"^(.+)(?:\s+for\s+|=)(.+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DependentRegex = new Regex(
                @"^(?:(.+);)?\s*(?:(not available)|(only)) with ([^;]+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex HiddenRegex = new Regex(
                @"^(?:(.+);)?\s*hidden with ([^;]+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex VisibleRegex = new Regex(
                @"^(?:(.+);)?\s*visible with ([^;]+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex IsNewRegex = new Regex(
                @"^(?:(.+);)?\s*new\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DisabledRegex = new Regex(
                @"^(?:0|off|disabled|false|no|none)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex FileRegex = new Regex(
                @"^((?:local|global)\s+)?(?:(dir|directory|folder|path)|(?:file|filename)(?:\s+\((.+)\))?)(?:,\s*relative\s+to\s+(___sub\w+))?$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PluginRegex = new Regex(
                @"^look for (\S+) in (\S+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal class CustomBooleanTestEntry : ITestEntry {
            private readonly bool _value;

            public CustomBooleanTestEntry(bool b) {
                _value = b;
            }

            public void Set(ITestEntryFactory factory) { }

            public bool Test(bool value) {
                return value == _value;
            }

            public bool Test(double value) {
                return _value != Equals(value, 0.0);
            }

            public bool Test(string value) {
                return Test(value != null && !DisabledRegex.IsMatch(value));
            }

            public bool Test(TimeSpan value) {
                return Test(value > default(TimeSpan));
            }

            public bool Test(DateTime value) {
                return Test(value > default(DateTime));
            }
        }

        protected PythonAppConfigValue() { }

        [CanBeNull]
        private string _originalValue;

        public void Set(string key, string value, string name, string toolTip,
                Func<IPythonAppConfigValueProvider, bool> isEnabledTest, Func<IPythonAppConfigValueProvider, bool> isHiddenTest,
                bool isNew, string originalValue) {
            OriginalKey = key;
            DisplayName = name;
            ToolTip = toolTip ?? key;
            Value = value;
            IsEnabledTest = isEnabledTest;
            IsHiddenTest = isHiddenTest;
            IsNew = isNew;
            _originalValue = originalValue;
            _resetCommand?.RaiseCanExecuteChanged();
            IsResettable = _originalValue != null;
            OnValueChanged();
        }

        public virtual void SetFilesRelativeDirectory(string directory) {
            FilesRelativeDirectory = directory;
        }

        public bool IsChanged => _originalValue != Value;
        public bool IsNonDefault => _originalValue != null && _originalValue != Value;

        public void Reset() {
            if (_originalValue == null) return;
            Value = _originalValue;
        }

        private bool _isResettable;

        public bool IsResettable {
            get => _isResettable;
            set => Apply(value, ref _isResettable);
        }

        private DelegateCommand _resetCommand;

        public ICommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(Reset, () => _originalValue != null));

        [CanBeNull]
        public static IPythonAppConfigValue Create([NotNull] PythonAppConfigParams configParams, KeyValuePair<string, string> pair,
                [CanBeNull] string commentary, [CanBeNull] string actualValue, bool isResetable) {
            string name = null, toolTip = null;
            Func<IPythonAppConfigValueProvider, bool> isEnabledTest = null;
            Func<IPythonAppConfigValueProvider, bool> isHiddenTest = null;
            var isNew = false;
            var result = CreateInner(pair, commentary, ref name, ref toolTip, ref isEnabledTest, ref isHiddenTest, ref isNew);
            if (result == null) return null;

            if (string.IsNullOrEmpty(name)) {
                name = PythonAppConfig.ConvertKeyToName(pair.Key);
            }

            result.SetFilesRelativeDirectory(configParams.FilesRelativeDirectory);
            result.Set(pair.Key, actualValue ?? pair.Value, name, toolTip, isEnabledTest, isHiddenTest, isNew, isResetable ? pair.Value : null);
            return result;
        }

        private class TesterInner : ITester<IPythonAppConfigValueProvider> {
            private readonly Func<string, string> _unwrap;

            public TesterInner(Func<string, string> unwrap) {
                _unwrap = unwrap;
            }

            public string ParameterFromKey(string key) {
                return key;
            }

            public bool Test(IPythonAppConfigValueProvider obj, string key, ITestEntry value) {
                return key == null || value.Test(obj.GetValue(_unwrap(key)));
            }
        }

        public static Func<IPythonAppConfigValueProvider, bool> CreateDisabledFunc(string query, bool invert, Func<string, string> unwrap) {
            query = Regex.Replace(query, @"\b(and|or|not)\b", m => {
                switch (m.Value) {
                    case "and":
                        return "&";
                    case "or":
                        return "|";
                    case "not":
                        return "!";
                }
                return m.Value;
            });

            var filter = Filter.Create(new TesterInner(unwrap), query, new FilterParams {
                StringMatchMode = StringMatchMode.StartsWith,
                BooleanTestFactory = b => new CustomBooleanTestEntry(b),
                ValueSplitter = new ValueSplitter(ValueSplitFunc.Custom, ValueSplitFunc.Separators),
                ValueConversion = null
            });

            if (invert) return p => !filter.Test(p);
            return filter.Test;
        }

        private static Func<IPythonAppConfigValueProvider, bool> CreateHiddenFunc(string query, bool invert, Func<string, string> unwrap) {
            query = Regex.Replace(query, @"\b(and|or|not)\b", m => {
                switch (m.Value) {
                    case "and":
                        return "&";
                    case "or":
                        return "|";
                    case "not":
                        return "!";
                }
                return m.Value;
            });

            var filter = Filter.Create(new TesterInner(unwrap), query, new FilterParams {
                StringMatchMode = StringMatchMode.StartsWith,
                BooleanTestFactory = b => new CustomBooleanTestEntry(b),
                ValueSplitter = new ValueSplitter(ValueSplitFunc.Custom, ValueSplitFunc.Separators),
                ValueConversion = null
            });

            if (invert) return p => !filter.Test(p);
            return filter.Test;
        }

        internal static class ValueSplitFunc {
            private static readonly Regex ParsingRegex = new Regex(@"^(.+?)([:<>≥≤=+-])\s*", RegexOptions.Compiled);
            public static readonly char[] Separators = { ':', '<', '>', '≥', '≤', '=', '+', '-' };

            private static string ClearKey(string key) {
                return key?.Trim().Trim('"', '\'', '`', '“', '”');
            }

            public static FilterPropertyValue Custom(string s) {
                var match = ParsingRegex.Match(s);
                if (!match.Success) return new FilterPropertyValue(ClearKey(s), FilterComparingOperation.IsTrue);

                var key = match.Groups[1].Value;
                var operation = (FilterComparingOperation)match.Groups[2].Value[0];
                var value = s.Substring(match.Length);
                return new FilterPropertyValue(ClearKey(key), operation, ClearKey(value));
            }
        }

        [CanBeNull]
        private static IPythonAppConfigValue CreateInner(KeyValuePair<string, string> pair, [CanBeNull] string commentary, [CanBeNull] ref string name,
                [CanBeNull] ref string toolTip, [CanBeNull] ref Func<IPythonAppConfigValueProvider, bool> isEnabledTest,
                [CanBeNull] ref Func<IPythonAppConfigValueProvider, bool> isHiddenTest, ref bool isNew) {
            var value = pair.Value;
            if (commentary != null) {
                var match = ValueCommentaryRegex.Match(commentary);
                if (match.Success) {
                    name = PythonAppConfig.CapitalizeFirst(match.Groups[1].Value.TrimEnd());
                    toolTip = match.Groups[2].Success ? PythonAppConfig.CapitalizeFirst(match.Groups[2].Value.Replace(@"; ", ";\n")) : null;

                    if (toolTip == null) {
                        toolTip = name;
                        if (name.Length > 50) {
                            name = null;
                        }
                    }

                    if (match.Groups[3].Success) {
                        var description = match.Groups[3].Value.Trim().WrapQuoted(out var unwrap);
                        if (description == "hidden") {
                            return null;
                        }

                        var dependent = DependentRegex.Match(description);
                        if (dependent.Success) {
                            description = dependent.Groups[1].Value;
                            isEnabledTest = CreateDisabledFunc(dependent.Groups[4].Value.Trim(), dependent.Groups[2].Success, unwrap);
                        }

                        var hidden = HiddenRegex.Match(description);
                        if (hidden.Success) {
                            description = hidden.Groups[1].Value;
                            isHiddenTest = CreateHiddenFunc(hidden.Groups[2].Value.Trim(), false, unwrap);
                        }

                        var visible = VisibleRegex.Match(description);
                        if (visible.Success) {
                            description = visible.Groups[1].Value;
                            isHiddenTest = CreateHiddenFunc(visible.Groups[2].Value.Trim(), true, unwrap);
                        }

                        var isNewMatch = IsNewRegex.Match(description);
                        if (isNewMatch.Success) {
                            description = isNewMatch.Groups[1].Value;
                            isNew = true;
                        }

                        if (NumberRegex.IsMatch(description)) {
                            return new PythonAppConfigNumberValue();
                        }

                        if (KeyRegex.IsMatch(description)) {
                            return new PythonAppConfigKeyValue();
                        }

                        var range = RangeRegex.Match(description);
                        if (range.Success) {
                            var r = new PythonAppConfigRangeValue(
                                    PythonAppReferencedValue<double>.Parse(unwrap(range.Groups[1].Value)),
                                    PythonAppReferencedValue<double>.Parse(unwrap(range.Groups[3].Value)),
                                    range.Groups[2].Success ? unwrap(range.Groups[2].Value) : null);
                            if (r.Postfix == null && range.Groups[4].Success) {
                                r.Postfix = "%";
                                r.DisplayMultiplier = 100d;
                            }
                            if (range.Groups[5].Success) {
                                r.RoundTo = FlexibleParser.TryParseDouble(range.Groups[5].Value) ?? 1d;
                                r.Tick = r.Tick.Ceiling(r.RoundTo);
                            } else if (range.Groups[6].Success) {
                                r.RoundTo = 1d;
                                r.Tick = Math.Ceiling(r.Tick);
                            }
                            return r;
                        }

                        var file = FileRegex.Match(description);
                        if (file.Success) {
                            return new PythonAppConfigFileValue(file.Groups[2].Success,
                                    file.Groups[1].Value.Contains("local", StringComparison.OrdinalIgnoreCase)
                                            ? PythonAppConfigFileValue.AbsoluteMode.Disallow
                                            : file.Groups[1].Value.Contains("global", StringComparison.OrdinalIgnoreCase)
                                                    ? PythonAppConfigFileValue.AbsoluteMode.Require
                                                    : PythonAppConfigFileValue.AbsoluteMode.Allow,
                                    file.Groups[3].Success ? unwrap(file.Groups[3].Value) : null,
                                    file.Groups[4].Success ? unwrap(file.Groups[4].Value) : null);
                        }

                        var plugin = PluginRegex.Match(description);
                        if (plugin.Success) {
                            return new PythonAppConfigPluginValue(plugin.Groups[2].Value, plugin.Groups[1].Value);
                        }

                        if (description.IndexOf(',') != -1) {
                            var options = OptionsRegex.Matches(description).Cast<Match>()
                                    .Select(x => (x.Groups[1].Success ? x.Groups[1].Value : x.Groups[2].Value).Trim()).ToArray();
                            if (options.Length > 0) {
                                return new PythonAppConfigOptionsValue(options.Select(x => {
                                    if (x == "__separator__") {
                                        return (object)new Separator();
                                    }
                                    
                                    var m1 = OptionValueAltRegex.Match(x);
                                    if (m1.Success) {
                                        return new SettingEntry(unwrap(m1.Groups[1].Value.TrimStart()),
                                                PythonAppConfig.CapitalizeFirst(unwrap(m1.Groups[2].Value.TrimEnd())));
                                    }

                                    var m2 = OptionValueRegex.Match(x);
                                    if (m2.Success) {
                                        return new SettingEntry(unwrap(m2.Groups[2].Value.TrimStart()),
                                                PythonAppConfig.CapitalizeFirst(unwrap(m2.Groups[1].Value.TrimEnd())));
                                    }

                                    x = unwrap(x);
                                    return new SettingEntry(x, AcStringValues.NameFromId(x.ToLowerInvariant()));
                                }).ToArray());
                            }
                        }

                        var boolean = BooleanRegex.Match(description);
                        if (boolean.Success) {
                            return new PythonAppConfigBoolValue(unwrap(boolean.Groups[1].Value), unwrap(boolean.Groups[2].Value));
                        }
                    }
                }
            }

            switch (value) {
                case "True":
                case "False":
                    return new PythonAppConfigBoolValue();

                case "true":
                case "false":
                    return new PythonAppConfigBoolValue("true", "false");

                case "TRUE":
                case "FALSE":
                    return new PythonAppConfigBoolValue("TRUE", "FALSE");

                case "On":
                case "Off":
                    return new PythonAppConfigBoolValue("On", "Off");

                case "on":
                case "off":
                    return new PythonAppConfigBoolValue("on", "off");

                case "ON":
                case "OFF":
                    return new PythonAppConfigBoolValue("ON", "OFF");
            }

            return new PythonAppConfigValue();
        }

        public virtual void UpdateReferenced(IPythonAppConfigValueProvider provider) {
            if (IsEnabledTest != null) {
                IsEnabled = IsEnabledTest(provider);
            }
            if (IsHiddenTest != null) {
                IsHidden = IsHiddenTest(provider);
            }
        }
    }
}