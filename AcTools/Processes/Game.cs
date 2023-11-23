﻿using AcTools.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using AcTools.Windows.Input;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcTools.Processes {
    public partial class Game {
        public static bool OptionEnableRaceIniRestoration = false;
        public static bool OptionRaceIniTestMode = false;
        public static bool OptionReplaySupportsFullPaths = false;

        public static void ClearUpIniFile(IniFile file) {
            file["BENCHMARK"].Set("ACTIVE", false);
            file["REPLAY"].Set("ACTIVE", false);
            file["REMOTE"].Set("ACTIVE", false);
            file["REMOTE"].Remove("__FEATURES");
            file["REMOTE"].Remove("__CM_EXTENDED");
            file["RESTART"].Set("ACTIVE", false);
            file["__PREVIEW_GENERATION"].Set("ACTIVE", false);
            file["LIGHTING"].Remove("__CM_WEATHER_TYPE");
            file["LIGHTING"].Remove("__CM_WEATHER_CONTROLLER");
            file["LIGHTING"].Remove("__CM_DATE");
            file["LIGHTING"].Remove("__CM_DATE_USE_TIME");
            file["LIGHTING"].Remove("__CM_WEATHER_HUMIDITY");
            file["LIGHTING"].Remove("__CM_WEATHER_PRESSURE");
            file["RACE"].Remove("__CM_CUSTOM_MODE");
            file["OPTIONS"].Remove("__BACKGROUND_IMAGE");

            file.RemoveSections("CAR", 1); // because CAR_0 is a player’s car
            file.RemoveSections("SESSION");
            file.RemoveSections("CONDITION");

            file.Remove("EVENT");
            file.Remove("SPECIAL_EVENT");
        }

        private static void SetDefaultProperies(IniFile file) {
            file["HEADER"].Set("VERSION", 2);
            file["LAP_INVALIDATOR"].Set("ALLOWED_TYRES_OUT", -1);
        }

        public static bool OptionDebugMode = false;

        [CanBeNull]
        public static Result GetResult(DateTime gameStartTime) {
            try {
                var filename = OptionDebugMode ? AcPaths.GetResultJsonFilename().Replace("race_out", "race_out_debug") : AcPaths.GetResultJsonFilename();
                var info = new FileInfo(filename);
                if (!info.Exists || info.LastWriteTime < gameStartTime) return null;
                var result = JsonConvert.DeserializeObject<Result>(FileUtils.ReadAllText(filename));
                return result?.IsNotCancelled == true ? result : null;
            } catch (Exception e) {
                throw new Exception("Can’t parse “race_out.json”", e);
            }
        }

        private static void RemoveResultJson() {
            FileUtils.TryToDelete(AcPaths.GetResultJsonFilename());
        }

        private static bool _busy;

        [CanBeNull]
        public static Result Start([NotNull] IAcsStarter starter, [NotNull] StartProperties properties) {
            if (starter == null) throw new ArgumentNullException(nameof(starter));
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            if (_busy) return null;
            _busy = true;

            RemoveResultJson();
            var start = DateTime.Now;

            try {
                properties.Set();
                if (OptionRaceIniTestMode) return null;

                starter.Run();
                starter.WaitUntilGame();
                starter.WaitGame();
            } finally {
                starter.CleanUp();

                _busy = false;
                properties.RevertChanges();
            }

            return GetResult(start);
        }

        public enum ProgressState {
            Preparing,
            Launching,
            Waiting,
            Finishing
        }

        [ItemCanBeNull]
        public static async Task<Result> StartAsync(IAcsStarter starter, StartProperties properties, IProgress<ProgressState> progress = null,
                CancellationToken cancellation = default) {
            if (_busy) return null;
            _busy = true;

            AcToolsLogging.Write("Starting AC: " + starter);
            AcToolsLogging.Write("  Debug mode: " + OptionDebugMode);
            if (OptionDebugMode) {
                progress?.Report(ProgressState.Waiting);
                await Task.Delay(500, cancellation);
                _busy = false;
                return GetResult(DateTime.MinValue);
            }

            RemoveResultJson();
            IKeyboardListener listener = null;

            if (properties.SetKeyboardListener) {
                try {
                    listener = KeyboardListenerFactory.Get();
                    listener.Subscribe();
                } catch (Exception e) {
                    AcToolsLogging.Write("Can’t set listener: " + e);
                }
            }

            var start = DateTime.Now;
            try {
                progress?.Report(ProgressState.Preparing);
                await Task.Run(() => properties.Set(), cancellation);
                if (cancellation.IsCancellationRequested || OptionRaceIniTestMode) return null;

                while (true) {
                    progress?.Report(ProgressState.Launching);
                    AcToolsLogging.Write("  Starting: " + starter);
                    await starter.RunAsync(cancellation);
                    AcToolsLogging.Write("  Started: " + starter);
                    if (cancellation.IsCancellationRequested) return null;

                    AcToolsLogging.Write("  Waiting: " + starter);
                    var process = await starter.WaitUntilGameAsync(cancellation);
                    AcToolsLogging.Write("  Waited: " + starter);
                    await Task.Run(() => properties.SetGame(process), cancellation);
                    if (cancellation.IsCancellationRequested) return null;

                    progress?.Report(ProgressState.Waiting);
                    await starter.WaitGameAsync(cancellation);
                    if (cancellation.IsCancellationRequested) return null;

                    var raceIni = new IniFile(AcPaths.GetRaceIniFilename());
                    if (raceIni["RESTART"].GetBool("ACTIVE", false)) {
                        raceIni["RESTART"].Set("ACTIVE", false);
                        raceIni.Save();
                    } else {
                        break;
                    }
                }
            } finally {
                _busy = false;

                if (cancellation.IsCancellationRequested) {
                    starter.CleanUp();
                } else {
                    progress?.Report(ProgressState.Finishing);
                    await starter.CleanUpAsync(cancellation);
                }

                properties.RevertChanges();
                listener?.Dispose();
            }

            return GetResult(start);
        }

        public abstract class AdditionalProperties {
            /// <summary>
            /// Set properties.
            /// </summary>
            /// <returns>Something disposable what will revert back all changes (optionally)</returns>
            [CanBeNull]
            public abstract IDisposable Set();
        }

        public abstract class GameHandler {
            /// <summary>
            /// Do something with runned game (but first, wait for game to run).
            /// </summary>
            /// <returns>Something disposable what will revert back all changes (optionally)</returns>
            [CanBeNull]
            public abstract IDisposable Set([CanBeNull] Process process);
        }

        public abstract class RaceIniProperties {
            /// <summary>
            /// Set properties.
            /// </summary>
            /// <param name="file">Main ini-file (race.ini)</param>
            public abstract void Set(IniFile file);
        }

        public static IniFile DefaultRaceConfig => new IniFile {
            ["HEADER"] = { ["VERSION"] = 2 },
            ["RACE"] = {
                ["TRACK"] = @"magione",
                ["CONFIG_TRACK"] = "",
                ["MODEL"] = @"lotus_elise_sc",
                ["MODEL_CONFIG"] = "",
                ["CARS"] = 1,
                ["AI_LEVEL"] = 98,
                ["FIXED_SETUP"] = 0,
                ["PENALTIES"] = 0
            },
            ["REMOTE"] = {
                ["ACTIVE"] = false,
                ["SERVER_IP"] = "",
                ["SERVER_PORT"] = "",
                ["NAME"] = "",
                ["TEAM"] = "",
                ["GUID"] = "",
                ["REQUESTED_CAR"] = "",
                ["PASSWORD"] = ""
            },
            ["CAR_0"] = {
                ["MODEL"] = @"-",
                ["MODEL_CONFIG"] = "",
                ["SKIN"] = @"Racing_Green_Stripe",
                ["DRIVER_NAME"] = "",
                ["NATIONALITY"] = @"ITA",
                ["AI_LEVEL"] = 96
            },
            ["GHOST_CAR"] = { ["RECORDING"] = true, ["PLAYING"] = true, ["SECONDS_ADVANTAGE"] = 0, ["LOAD"] = true, ["FILE"] = "" },
            ["REPLAY"] = { ["FILENAME"] = "", ["ACTIVE"] = false },
            ["LIGHTING"] = { ["SUN_ANGLE"] = -48, ["TIME_MULT"] = 1, ["CLOUD_SPEED"] = 0.2 },
            ["GROOVE"] = { ["VIRTUAL_LAPS"] = 10, ["MAX_LAPS"] = 30, ["STARTING_LAPS"] = 0 },
            ["DYNAMIC_TRACK"] = { ["SESSION_START"] = 100, ["SESSION_TRANSFER"] = 50, ["RANDOMNESS"] = 0, ["LAP_GAIN"] = 1 },
            ["LAP_INVALIDATOR"] = { ["ALLOWED_TYRES_OUT"] = -1 },
            ["TEMPERATURE"] = { ["AMBIENT"] = 26, ["ROAD"] = 32 },
            ["WEATHER"] = { ["NAME"] = @"4_mid_clear" },
        };

        public class StartProperties {
            /// <summary>
            /// I can’t quite explain this, but with initializing KeyboardListener from
            /// AcManager.Tools, they don’t work. So, at least as a temporary solution,
            /// I’m going to initialize another listener from AcTools itself for
            /// the duration of the race. All of them will share single hook anyway.
            /// </summary>
            /// <remarks>
            /// BUG: What the hell is that shit‽ When did I write that? Holy crap, what a mess.
            /// TODO: Figure out if this is still the problem and, if so, solve it. And remove it.
            /// </remarks>
            public bool SetKeyboardListener;

            [CanBeNull]
            public IniFile PreparedConfig;

            [CanBeNull]
            public BasicProperties BasicProperties;

            [CanBeNull]
            public AssistsProperties AssistsProperties;

            [CanBeNull]
            public ConditionProperties ConditionProperties;

            [CanBeNull]
            public TrackProperties TrackProperties;

            [CanBeNull]
            public BaseModeProperties ModeProperties;

            [CanBeNull]
            public ReplayProperties ReplayProperties;

            [CanBeNull]
            public BenchmarkProperties BenchmarkProperties;

            [ItemCanBeNull]
            public List<object> AdditionalPropertieses = new List<object>();

            public DateTime StartTime { get; internal set; }

            public void SetAdditional<T>(T properties) {
                AdditionalPropertieses.Remove(GetAdditional<T>());
                if (properties == null) return;
                AdditionalPropertieses.Add(properties);
            }

            [CanBeNull]
            public T GetAdditional<T>() {
                return AdditionalPropertieses.OfType<T>().FirstOrDefault();
            }

            [CanBeNull]
            public T PullAdditional<T>() {
                var existing = GetAdditional<T>();
                if (existing != null) {
                    AdditionalPropertieses.Remove(existing);
                }

                return existing;
            }

            public bool RemoveAdditional<T>() {
                return AdditionalPropertieses.Remove(GetAdditional<T>());
            }

            public bool HasAdditional<T>() {
                return AdditionalPropertieses.OfType<T>().Any();
            }

            private List<IDisposable> _disposeLater;
            private List<string> _removeLater;

            public StartProperties() { }

            public StartProperties(BenchmarkProperties benchmarkProperties) {
                BenchmarkProperties = benchmarkProperties;
            }

            public StartProperties(ReplayProperties replayProperties) {
                ReplayProperties = replayProperties;
            }

            public StartProperties(BasicProperties basicProperties, AssistsProperties assistsProperties, ConditionProperties conditionProperties,
                    TrackProperties trackProperties, BaseModeProperties modeProperties) {
                BasicProperties = basicProperties;
                AssistsProperties = assistsProperties;
                ConditionProperties = conditionProperties;
                TrackProperties = trackProperties;
                ModeProperties = modeProperties;
            }

            internal void Set() {
                _disposeLater = new List<IDisposable>();
                _removeLater = new List<string>();

                var iniFilename = AcPaths.GetRaceIniFilename();
                if (OptionEnableRaceIniRestoration) {
                    _disposeLater.Add(FileUtils.RestoreLater(iniFilename));
                }

                var iniFile = PreparedConfig;
                if (iniFile == null) {
                    iniFile = new IniFile(iniFilename);

                    ClearUpIniFile(iniFile);
                    SetDefaultProperies(iniFile);

                    if (BasicProperties != null) {
                        BasicProperties?.Set(iniFile);
                        ModeProperties?.Set(iniFile);
                        ConditionProperties?.Set(iniFile);
                        TrackProperties?.Set(iniFile);
                    } else if (ReplayProperties != null) {
                        if (ReplayProperties.Name == null && ReplayProperties.Filename != null) {
                            var dir = AcPaths.GetReplaysDirectory();
                            if (FileUtils.IsAffectedBy(ReplayProperties.Filename, dir)) {
                                ReplayProperties.Name = FileUtils.GetRelativePath(ReplayProperties.Filename, dir);
                            } else if (OptionReplaySupportsFullPaths) {
                                ReplayProperties.Name = ReplayProperties.Filename;
                            } else {
                                var removeLaterFilename = FileUtils.GetTempFileName(dir, ".tmp");
                                ReplayProperties.Name = Path.GetFileName(removeLaterFilename);
                                File.Copy(ReplayProperties.Filename, removeLaterFilename);
                                _removeLater.Add(removeLaterFilename);
                            }
                        }

                        ReplayProperties.Set(iniFile);
                    } else if (BenchmarkProperties != null) {
                        BenchmarkProperties.Set(iniFile);
                    } else {
                        throw new NotSupportedException();
                    }
                }

                foreach (var properties in AdditionalPropertieses.OfType<RaceIniProperties>()) {
                    properties.Set(iniFile);
                }

                iniFile.Save(iniFilename);

                _disposeLater.AddRange(AdditionalPropertieses.OfType<AdditionalProperties>().Select(x => x.Set())
                        .Prepend(AssistsProperties?.Set()).NonNull());

                StartTime = DateTime.Now;
            }

            internal void SetGame([CanBeNull] Process process) {
                _disposeLater.AddRange(AdditionalPropertieses.OfType<GameHandler>().Select(x => {
                    try {
                        return x.Set(process);
                    } catch (Exception e) {
                        AcToolsLogging.Write(e);
                        return null;
                    }
                }).NonNull());
            }

            internal void RevertChanges() {
                AdditionalPropertieses.OfType<IDisposable>().DisposeEverything();

                _disposeLater?.DisposeEverything();
                if (_removeLater == null) return;

                foreach (var filename in _removeLater) {
                    try {
                        File.Delete(filename);
                    } catch (Exception) {
                        // ignored
                    }
                }
                _removeLater.Clear();
            }

            public string GetDescription() {
                return
                        $"(Basic={(BasicProperties != null ? "Race" : ReplayProperties != null ? "Replay" : BenchmarkProperties != null ? "Benchmark" : "Unknown")}, "
                                + $"Mode={ModeProperties?.GetType().Name}, Additional=[{AdditionalPropertieses.Select(x => x?.GetType().Name).JoinToString(", ")}])";
            }
        }
    }
}