using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.ContentInstallation.Installators;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public abstract class ContentEntryBase : NotifyPropertyChanged {
        [CanBeNull]
        private readonly string[] _cleanUp;

        public static event EventHandler OnInstallationModeChanged;

        [NotNull]
        public string Id { get; }

        public string DisplayId => string.IsNullOrEmpty(Id) ? "N/A" : Id;

        /// <summary>
        /// Empty if object’s in root.
        /// </summary>
        [NotNull]
        public string EntryPath { get; }

        [NotNull]
        public string DisplayPath => string.IsNullOrEmpty(EntryPath) ? "N/A" : Path.DirectorySeparatorChar + EntryPath;

        [NotNull]
        public string Name { get; }

        public abstract double Priority { get; }

        [CanBeNull]
        public string Version { get; private set; }

        [CanBeNull]
        public byte[] IconData { get; protected set; }

        [CanBeNull]
        public string Description { get; }

        private bool _showWarning;

        public bool ShowWarning {
            get => _showWarning;
            set => Apply(value, ref _showWarning);
        }

        private bool _singleEntry;

        public bool SingleEntry {
            get => _singleEntry;
            set => Apply(value, ref _singleEntry);
        }

        private bool _installAsGenericMod;

        public bool InstallAsGenericMod {
            get => _installAsGenericMod;
            set => Apply(value, ref _installAsGenericMod);
        }

        private bool _enableAfterwards = true;

        public bool EnableAfterwards {
            get => _enableAfterwards;
            set => Apply(value, ref _enableAfterwards);
        }

        public virtual bool NeedsToBeEnabled => false;

        public bool GenericModSupported => GenericModSupportedByDesign && _installationParams?.CupType.HasValue != true;
        protected abstract bool GenericModSupportedByDesign { get; }

        [CanBeNull]
        public abstract string GenericModTypeName { get; }

        public abstract string NewFormat { get; }

        public abstract string ExistingFormat { get; }

        protected ContentEntryBase(bool showWarning, [NotNull] string path, [NotNull] string id, [CanBeNull] string[] cleanUp,
                string name = null, string version = null, byte[] iconData = null, string description = null) {
            _cleanUp = cleanUp;
            EntryPath = path ?? throw new ArgumentNullException(nameof(path));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? id;
            Version = version;
            IconData = iconData;
            Description = description;
            ShowWarning = showWarning;
        }

        private bool _installEntry;

        public bool InstallEntry {
            get => _installEntry;
            set => Apply(value, ref _installEntry);
        }

        private IEnumerable<string> CleanUpBase(string location) {
            return _cleanUp?.Select(x => Path.Combine(x, location)) ?? new string[0];
        }

        private void SetFallbackCleanUp() {
            if (_cleanUp != null) {
                foreach (var option in _updateOptions.Where(x => !x.RemoveExisting && x.CleanUp == null)) {
                    option.CleanUp = CleanUpBase;
                }
            }
        }

        private void InitializeOptions() {
            if (_updateOptions == null) {
                var oldValue = _selectedOption;
                _updateOptions = GetUpdateOptions().ToArray();
                SetFallbackCleanUp();
                _selectedOption = GetDefaultUpdateOption(_updateOptions);
                OnSelectedOptionChanged(oldValue, _selectedOption);
            }
        }

        protected void ResetUpdateOptions() {
            var oldValue = _selectedOption;
            _updateOptions = GetUpdateOptions().ToArray();
            SetFallbackCleanUp();
            _selectedOption = GetDefaultUpdateOption(_updateOptions);
            OnSelectedOptionChanged(oldValue, _selectedOption);
            OnPropertyChanged(nameof(UpdateOptions));
            OnPropertyChanged(nameof(SelectedOption));
        }

        private ContentInstallationParams _installationParams;

        public void SetInstallationParams([NotNull] ContentInstallationParams installationParams) {
            _installationParams = installationParams;
            if (_installationParams.CupType.HasValue && _installationParams.Version != null) {
                Version = _installationParams.Version;
            }
        }

        protected virtual UpdateOption GetDefaultUpdateOption(UpdateOption[] list) {
            return _installationParams?.PreferCleanInstallation == true
                    ? (list.FirstOrDefault(x => x.RemoveExisting) ?? list.FirstOrDefault())
                    : list.FirstOrDefault();
        }

        private UpdateOption _selectedOption;

        [CanBeNull]
        public UpdateOption SelectedOption {
            get {
                InitializeOptions();
                return _selectedOption;
            }
            set {
                if (Equals(value, _selectedOption)) return;
                var oldValue = _selectedOption;
                _selectedOption = value;
                OnSelectedOptionChanged(oldValue, value);
                OnPropertyChanged();

                if (Keyboard.Modifiers == ModifierKeys.Control) {
                    OnInstallationModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string GetNew(string displayName) {
            return string.Format(NewFormat, displayName);
        }

        public string GetExisting(string displayName) {
            return string.Format(ExistingFormat, displayName);
        }

        private UpdateOption[] _updateOptions;

        public IReadOnlyList<UpdateOption> UpdateOptions {
            get {
                InitializeOptions();
                return _updateOptions;
            }
        }

        protected virtual void OnSelectedOptionChanged(UpdateOption oldValue, UpdateOption newValue) { }

        protected virtual IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] {
                new UpdateOption(ToolsStrings.Installator_UpdateEverything, false),
                new UpdateOption(ToolsStrings.Installator_RemoveExistingFirst, true)
            };
        }

        protected bool MoveEmptyDirectories = false;

        protected virtual ICopyCallback GetCopyCallback([NotNull] string destination) {
            var filter = SelectedOption?.Filter;
            var path = EntryPath;
            return new CopyCallback(info => {
                var filename = info.Key;
                if (path != string.Empty && !FileUtils.IsAffectedBy(filename, path)) return null;

                var subFilename = FileUtils.GetRelativePath(filename, path);
                return filter == null || filter(subFilename) ? Path.Combine(destination, subFilename) : null;
            }, MoveEmptyDirectories ? (info => {
                var filename = info.Key;
                if (path != string.Empty && !FileUtils.IsAffectedBy(filename, path)) return null;

                var subFilename = FileUtils.GetRelativePath(filename, path);
                return filter == null || filter(subFilename) ? Path.Combine(destination, subFilename) : null;
            }) : (Func<IDirectoryInfo, string>)null);
        }

        protected virtual Task EnableAfterInstallation(CancellationToken token) {
            return Task.Delay(0);
        }

        [ItemCanBeNull]
        public async Task<InstallationDetails> GetInstallationDetails(CancellationToken cancellation) {
            var destination = await GetDestination(cancellation);
            var selectedOptionAfterTask = SelectedOption?.AfterTask;
            if (IsNew && NeedsToBeEnabled && EnableAfterwards) {
                if (selectedOptionAfterTask == null) {
                    selectedOptionAfterTask = EnableAfterInstallation;
                } else {
                    var optionTask = selectedOptionAfterTask;
                    selectedOptionAfterTask = async token => {
                        await optionTask(token);
                        await EnableAfterInstallation(token);
                    };
                }
            }

            return destination != null ?
                    new InstallationDetails(GetCopyCallback(destination),
                            GetFilesToRemoval(destination),
                            SelectedOption?.BeforeTask,
                            selectedOptionAfterTask) {
                                OriginalEntry = this
                            } :
                    null;
        }

        [CanBeNull]
        protected virtual string[] GetFilesToRemoval([NotNull] string destination) {
            return SelectedOption?.CleanUp?.Invoke(destination)?.ToArray();
        }

        [ItemCanBeNull]
        protected abstract Task<string> GetDestination(CancellationToken cancellation);

        private BetterImage.Image? _icon;

        public BetterImage.Image? Icon => IconData == null ? null :
                _icon ?? (_icon = BetterImage.LoadBitmapSourceFromBytes(IconData, 32));

        #region From Wrapper
        private bool _active = true;

        public bool Active {
            get => _active;
            set => Apply(value, ref _active);
        }

        private bool _noConflictMode;

        public bool NoConflictMode {
            get => _noConflictMode;
            set => Apply(value, ref _noConflictMode);
        }

        public async Task CheckExistingAsync() {
            var tuple = await GetExistingNameAndVersionAsync();
            IsNew = tuple == null;
            ExistingName = tuple?.Item1;
            ExistingVersion = tuple?.Item2;
            IsNewer = Version.IsVersionNewerThan(ExistingVersion);
            IsOlder = Version.IsVersionOlderThan(ExistingVersion);
        }

        [NotNull, ItemCanBeNull]
        protected abstract Task<Tuple<string, string>> GetExistingNameAndVersionAsync();

        public bool IsNew { get; set; }

        [CanBeNull]
        private string _existingVersion;

        [CanBeNull]
        public string ExistingVersion {
            get => _existingVersion;
            set {
                if (value == _existingVersion) return;
                _existingVersion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        [CanBeNull]
        private string _existingName;

        [CanBeNull]
        public string ExistingName {
            get => _existingName;
            set {
                if (value == _existingName) return;
                _existingName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        private bool _isNewer;

        public bool IsNewer {
            get => _isNewer;
            set => Apply(value, ref _isNewer);
        }

        private bool _isOlder;

        public bool IsOlder {
            get => _isOlder;
            set => Apply(value, ref _isOlder);
        }

        public string DisplayName => IsNew ? GetNew(Name) : GetExisting(ExistingName ?? Name);
        #endregion
    }

    public abstract class ContentEntryBase<T> : ContentEntryBase where T : AcCommonObject {
        protected ContentEntryBase(bool showWarning, [NotNull] string path, [NotNull] string id, [CanBeNull] string[] cleanUp,
                string name = null, string version = null, byte[] iconData = null)
                : base(showWarning, path, id, cleanUp, name, version, iconData) { }

        protected sealed override bool GenericModSupportedByDesign => IsNew;

        public abstract FileAcManager<T> GetManager();

        private T _acObjectNew;

        [ItemCanBeNull]
        public async Task<T> GetExistingAcObjectAsync() {
            return _acObjectNew ?? (_acObjectNew = await GetManager().GetByIdAsync(Id));
        }

        protected T GetExistingAcObject() {
            return _acObjectNew ?? (_acObjectNew = GetManager().GetById(Id));
        }

        protected override async Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            var obj = await GetExistingAcObjectAsync();
            return obj == null ? null : Tuple.Create(obj.DisplayName, (obj as IAcObjectVersionInformation)?.Version);
        }

        protected override async Task<string> GetDestination(CancellationToken cancellation) {
            var manager = GetManager();
            if (manager == null) return null;

            var destination = await manager.PrepareForAdditionalContentAsync(Id,
                    SelectedOption != null && SelectedOption.RemoveExisting);
            return cancellation.IsCancellationRequested ? null : destination;
        }
    }
}