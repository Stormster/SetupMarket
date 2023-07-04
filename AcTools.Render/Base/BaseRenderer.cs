﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Sprites;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Debug = System.Diagnostics.Debug;
using Device = SlimDX.Direct3D11.Device;
using Resource = SlimDX.Direct3D11.Resource;

namespace AcTools.Render.Base {
    public abstract class BaseRenderer : IDisposable, INotifyPropertyChanged {
        public static bool OptionDebugResources = true;
        public static readonly Color4 ColorTransparent = new Color4(0f, 0f, 0f, 0f);

        private DeviceContextHolder _deviceContextHolder;

        private void SetDeviceContextHolder(DeviceContextHolder value) {
            _deviceContextHolder = value;
            value.TimeFactor = TimeFactor;
            value.UpdateRequired += (sender, args) => {
                IsDirty = true;
            };
        }

        private SwapChainDescription _swapChainDescription;
        private SwapChain _swapChain;

        public Device Device => _deviceContextHolder.Device;
        public DeviceContext DeviceContext => _deviceContextHolder.DeviceContext;
        public DeviceContextHolder DeviceContextHolder => _deviceContextHolder;

        public bool IsDirty { get; set; }
        public virtual bool AccumulationMode => false;

        private int _width, _height;
        private bool _resized = true;
        private SpriteRenderer _sprite;

        [CanBeNull]
        protected SpriteRenderer Sprite => !UseSprite || _sprite != null || FeatureLevel < FeatureLevel.Level_10_0 ? _sprite :
                (_sprite = new SpriteRenderer(DeviceContextHolder));

        public bool SpriteInitialized => _sprite != null;

        private double _resolutionMultiplier = 1d;
        private double _previousResolutionMultiplier = 2d;

        /// <summary>
        /// Be careful with this option! Don’t forget to call PrepareForFinalPass() if you’re
        /// using it and remember — Z-buffer will be in original (bigger) size, but output buffer
        /// will be smaller.
        /// </summary>
        public double ResolutionMultiplier {
            get => _resolutionMultiplier;
            set {
                if (value < 0d) {
                    _previousResolutionMultiplier = -value;
                    return;
                }

                if (Equals(value, _resolutionMultiplier)) return;
                _resolutionMultiplier = value;

                if (!Equals(value, 1d)) {
                    _previousResolutionMultiplier = value;
                }

                OnResolutionMultiplierChanged();
                IsDirty = true;
            }
        }

        protected virtual void OnResolutionMultiplierChanged() {
            UpdateSampleDescription();

            _resized = true;
            IsDirty = true;
            OnPropertyChanged(nameof(ResolutionMultiplier));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(OutputDownscaleMultiplier));
            OnPropertyChanged(nameof(UseSsaa));
        }

        public bool UseSsaa {
            get => !Equals(1d, _resolutionMultiplier);
            set => ResolutionMultiplier = value ? _previousResolutionMultiplier : 1d;
        }

        public int ActualWidth => _width;

        public int Width {
            get => (int)(_width * ResolutionMultiplier);
            set {
                if (Equals(_width, value)) return;
                _width = value;
                _resized = true;
                IsDirty = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActualWidth));
            }
        }

        public int ActualHeight => _height;

        public int Height {
            get => (int)(_height * ResolutionMultiplier);
            set {
                if (Equals(_height, value)) return;
                _height = value;
                _resized = true;
                IsDirty = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActualHeight));
            }
        }

        public double OutputDownscaleMultiplier => 1 / ResolutionMultiplier;
        public float AspectRatio => (float)ActualWidth / ActualHeight;

        private readonly FrameMonitor _frameMonitor = new FrameMonitor();
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private float _previousFramesPerSecond;

        public float FramesPerSecond {
            get {
                var current = _frameMonitor.FramesPerSecond;
                if (Math.Abs(_previousFramesPerSecond - current) > 0.0001f) {
                    _previousFramesPerSecond = current;
                    OnPropertyChanged(nameof(FramesPerSecond));
                }
                return current;
            }
        }

        public virtual int AccumulatedFrame => 0;

        public float Elapsed => _stopwatch.ElapsedMilliseconds / 1000f;
        private float _previousElapsed;

        public void ResetDelta() {
            _previousElapsed = Elapsed;
        }

        protected BaseRenderer() {
#if DEBUG
            Configuration.EnableObjectTracking = OptionDebugResources;
            Configuration.DetectDoubleDispose = OptionDebugResources;
#else
            Configuration.EnableObjectTracking = false;
            Configuration.DetectDoubleDispose = false;
#endif
        }

        private SampleDescription GetMsaaDescription(Device device) {
            var msaaQuality = device.CheckMultisampleQualityLevels(Format.R16G16B16A16_Float, MsaaSampleCount);
            return new SampleDescription(MsaaSampleCount, msaaQuality - 1);
        }

        private bool _useMsaa;

        public bool UseMsaa {
            get => _useMsaa;
            set {
                if (value == _useMsaa) return;
                _useMsaa = value;
                OnPropertyChanged();
                UpdateSampleDescription();
                IsDirty = true;
            }
        }

        private int _msaaSampleCount = 4;

        public int MsaaSampleCount {
            get => _msaaSampleCount;
            set {
                if (Equals(value, _msaaSampleCount)) return;
                _msaaSampleCount = value;
                OnPropertyChanged();
                UpdateSampleDescription();
                IsDirty = true;
            }
        }

        private void UpdateSampleDescription() {
            if (_deviceContextHolder == null) return;

            var sampleDescription = UseMsaa ? GetMsaaDescription(Device) : new SampleDescription(1, 0);
            if (sampleDescription != SampleDescription) {
                SampleDescription = sampleDescription;
                if (_swapChain != null) {
                    RecreateSwapChain();
                } else {
                    _resized = true;
                }
            }
        }

        public bool WpfMode { get; set; }
        public bool UseSprite { get; set; } = true;
        protected bool Initialized { get; private set; }

        private SampleDescription _sampleDescription = new SampleDescription(1, 0);

        public SampleDescription SampleDescription {
            get => _sampleDescription;
            private set {
                if (value.Equals(_sampleDescription)) return;
                _sampleDescription = value;
                _deviceContextHolder?.OnResize(Width, Height, value);
                OnPropertyChanged();
            }
        }

        protected abstract FeatureLevel FeatureLevel { get; }

        protected DeviceCreationFlags DeviceCreationFlags { get; set; } = DeviceCreationFlags.None;

        /// <summary>
        /// Get Device (could be temporary, could be not), set proper SampleDescription
        /// </summary>
        private Device InitializeDevice() {
            Debug.Assert(Initialized == false);

            var device = new Device(DriverType.Hardware, DeviceCreationFlags);
            if (device.FeatureLevel < FeatureLevel) {
                throw new Exception($"Direct3D Feature {FeatureLevel} unsupported");
            }

            if (UseMsaa) {
                SampleDescription = GetMsaaDescription(device);
            }

            return device;
        }

        /// <summary>
        /// Initialize for out-screen rendering
        /// </summary>
        public void Initialize() {
            Debug.Assert(Initialized == false);

            SetDeviceContextHolder(new DeviceContextHolder(InitializeDevice()));
            InitializeInner();
            Initialized = true;
        }

        public IntPtr GetRenderTarget() {
            return _renderBuffer.ComPointer;
        }

        private bool _sharedHolder;

        /// <summary>
        /// Initialize for out-screen rendering using exising holder
        /// </summary>
        public void Initialize(DeviceContextHolder existingHolder) {
            Debug.Assert(Initialized == false);

            _sharedHolder = true;
            SetDeviceContextHolder(existingHolder);
            InitializeInner();
            Initialized = true;
        }

        /// <summary>
        /// Initialize for on-screen rendering
        /// </summary>
        public void Initialize(IntPtr outputHandle) {
            Debug.Assert(Initialized == false);

            if (UseMsaa) {
                InitializeDevice().Dispose();
            }

            _swapChainDescription = new SwapChainDescription {
                BufferCount = 2,
                ModeDescription = new ModeDescription(0, 0, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                IsWindowed = true,
                OutputHandle = outputHandle,
                SampleDescription = SampleDescription,
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };

            #if DEBUG
            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags | DeviceCreationFlags.Debug, _swapChainDescription, out var device, out _swapChain);
            #else
            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags, _swapChainDescription, out var device, out _swapChain);
            #endif
            SetDeviceContextHolder(new DeviceContextHolder(device));

            // Let Factory leak: otherwise we can’t dispose swap chain
            _swapChain.GetParent<Factory>().SetWindowAssociation(outputHandle, WindowAssociationFlags.IgnoreAll);

            InitializeInner();
            Initialized = true;
        }

        private void RecreateSwapChain() {
            _swapChainDescription = new SwapChainDescription {
                BufferCount = 2,
                ModeDescription = new ModeDescription(0, 0, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                IsWindowed = true,
                OutputHandle = _swapChainDescription.OutputHandle,
                SampleDescription = SampleDescription,
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };

            DisposeSwapChain();
            _swapChain = new SwapChain(Device.Factory, Device, _swapChainDescription);
            _resized = true;
        }

        protected abstract void InitializeInner();

        private Texture2D _renderBuffer;
        private RenderTargetView _renderView;
        private Texture2D _depthBuffer;
        private DepthStencilView _depthView;

        public Texture2D RenderBuffer => _renderBuffer;
        public Texture2D DepthBuffer => _depthBuffer;

        [CanBeNull]
        protected Tuple<Texture2D, DepthStencilView> CreateDepthBuffer(int width, int height) {
            try {
                var tex = new Texture2D(Device, new Texture2DDescription {
                    Format = FeatureLevel < FeatureLevel.Level_10_0 ? Format.D16_UNorm : Format.D32_Float,
                    ArraySize = 1,
                    MipLevels = 1,
                    Width = width,
                    Height = height,
                    SampleDescription = SampleDescription,
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.DepthStencil,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None
                });

                return Tuple.Create(tex, new DepthStencilView(Device, tex));
            } catch (Exception) {
                AcToolsLogging.Write($"{width}×{height}");
                throw;
            }
        }

        protected void Resize() {
            var width = Width;
            var height = Height;

            if (width == 0 || height == 0) return;

            lock (Device) {
                DisposeHelper.Dispose(ref _renderBuffer);
                DisposeHelper.Dispose(ref _renderView);
                DisposeHelper.Dispose(ref _depthBuffer);
                DisposeHelper.Dispose(ref _depthView);

                if (_swapChain != null) {
                    _swapChain.ResizeBuffers(_swapChainDescription.BufferCount, ActualWidth, ActualHeight,
                            Format.Unknown, SwapChainFlags.None);
                    _renderBuffer = Resource.FromSwapChain<Texture2D>(_swapChain, 0);
                } else {
                    _renderBuffer = new Texture2D(Device, new Texture2DDescription {
                        Width = ActualWidth,
                        Height = ActualHeight,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = WpfMode ? Format.B8G8R8A8_UNorm : Format.R8G8B8A8_UNorm,
                        SampleDescription = SampleDescription,
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                        CpuAccessFlags = CpuAccessFlags.None,
                        OptionFlags = WpfMode ? ResourceOptionFlags.Shared : ResourceOptionFlags.None
                    });
                }

                _renderView = new RenderTargetView(Device, _renderBuffer);

                var depth = CreateDepthBuffer(width, height);
                _depthBuffer = depth?.Item1;
                _depthView = depth?.Item2;

                DeviceContext.Rasterizer.SetViewports(OutputViewport);
                Sprite?.RefreshViewport();

                DeviceContext.Rasterizer.SetViewports(Viewport);

                ResetTargets();
                DeviceContext.OutputMerger.DepthStencilState = null;

                ResizeInner();
                _deviceContextHolder?.OnResize(Width, Height, SampleDescription);

                InitiallyResized = true;
            }
        }

        protected void PrepareForFinalPass() {
            DeviceContext.Rasterizer.SetViewports(OutputViewport);
        }

        protected bool InitiallyResized { get; private set; }

        public Viewport Viewport => new Viewport(0, 0, Width, Height, 0.0f, 1.0f);
        public Viewport OutputViewport => new Viewport(0, 0, ActualWidth, ActualHeight, 0.0f, 1.0f);

        protected abstract void ResizeInner();

        protected void ResetTargets() {
            DeviceContext.OutputMerger.SetTargets(_depthView, _renderView);
        }

        protected DepthStencilView DepthStencilView => _depthView;
        protected RenderTargetView RenderTargetView => _renderView;

        private float _timeFactor = 1f;

        public float TimeFactor {
            get => _timeFactor;
            set {
                if (value.Equals(_timeFactor)) return;
                _timeFactor = value;

                if (_deviceContextHolder != null) {
                    _deviceContextHolder.TimeFactor = value;
                }

                OnPropertyChanged();
            }
        }

        public bool IsPaused { get; set; }

        public virtual void Draw() {
            if (!IsPaused) {
                DrawInner();
            }
        }

        private bool DrawInner() {
            Debug.Assert(Initialized);
            IsDirty = false;

            var result = _resized;
            if (result) {
                Resize();
                _resized = false;
            }

            if (!_stopwatch.IsRunning) {
                _stopwatch.Start();
            }

            lock (Device) {
                var elapsed = Elapsed;
                var dt = (elapsed - _previousElapsed) * TimeFactor;

                if (dt > 0f) {
                    OnTick(dt);
                }

                _previousElapsed = elapsed;

                if (_frameMonitor.Tick()) {
                    OnPropertyChanged(nameof(FramesPerSecond));
                }

                DeviceContext.Rasterizer.SetViewports(Viewport);

                DrawOverride();
                if (SpriteInitialized) {
                    DrawSprites();
                }

                if (_swapChain != null) {
                    _swapChain.Present(SyncInterval ? 1 : 0, PresentFlags.None);
                } else {
                    DeviceContext.Flush();
                }
            }

            return result;
        }

        private bool _syncInterval = true;

        public bool SyncInterval {
            get => _syncInterval;
            set {
                if (Equals(value, _syncInterval)) return;
                _syncInterval = value;
                OnPropertyChanged();
            }
        }

        protected virtual void DrawOverride() {
            DeviceContext.ClearDepthStencilView(_depthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
            DeviceContext.ClearRenderTargetView(_renderView, Color.DarkCyan);
        }

        protected virtual void DrawSprites() {
            _sprite?.Flush();
        }

        public event TickEventHandler Tick;

        protected abstract void OnTickOverride(float dt);

        public void OnTick(float dt) {
            OnTickOverride(dt);
            DeviceContextHolder.OnTick(dt);
            Tick?.Invoke(this, new TickEventArgs(dt));
        }

        public void ExitFullscreen() {
            if (_swapChain == null) return;
            _swapChain.IsFullScreen = false;
        }

        public void EnterFullscreen() {
            if (_swapChain == null) return;
            _swapChain.IsFullScreen = true;
        }

        public void ToggleFullscreen() {
            if (_swapChain == null) return;
            _swapChain.IsFullScreen = !_swapChain.IsFullScreen;
        }

        protected void DisposeSwapChain() {
            Debug.WriteLine("DISPOSING SWAP CHAIN: " + _swapChain);
            try {
                DisposeHelper.Dispose(ref _swapChain);
                Debug.WriteLine("SWAPCHAIN DISPOSED");
            } catch (ObjectDisposedException) {
                Debug.WriteLine("SWAPCHAIN ALREADY DISPOSED?");
                _swapChain = null;
            } catch (Exception) {
                Debug.WriteLine("WUUT?");
                _swapChain = null;
            }
        }

        private WeakReference<Form> _associatedWindow;

        [CanBeNull]
        public Form AssociatedWindow {
            get {
                if (_associatedWindow != null) {
                    if (_associatedWindow.TryGetTarget(out var result)) return result;
                    _associatedWindow = null;
                }

                return null;
            }
        }

        public void SetAssociatedWindow(Form window) {
            _associatedWindow = window == null ? null : new WeakReference<Form>(window);
        }

        private bool _disposed;

        public bool Disposed {
            get => _disposed;
            private set {
                if (value == _disposed) return;
                _disposed = value;
                OnPropertyChanged();
            }
        }

        protected virtual void DisposeOverride() {}

        public void Dispose() {
            if (Disposed) {
                AcToolsLogging.Write("Already disposed!");
                return;
            }

            Disposed = true;

#if DEBUG
            // var debug = new SlimDX.Direct3D11.Debug(Device);
#endif
            DisposeOverride();

            DisposeHelper.Dispose(ref _renderView);
            DisposeHelper.Dispose(ref _renderBuffer);

            DisposeHelper.Dispose(ref _sprite);

            DisposeHelper.Dispose(ref _shotRenderBuffer);
            DisposeHelper.Dispose(ref _shotMsaaTemporaryTexture);
            DisposeHelper.Dispose(ref _shotDownsampleTexture);
            DisposeHelper.Dispose(ref _shotCutTexture);

            DisposeHelper.Dispose(ref _depthBuffer);
            DisposeHelper.Dispose(ref _depthView);
            
            if (!_sharedHolder) {
                _deviceContextHolder.Dispose(_swapChain);
                _deviceContextHolder = null;
                _swapChain = null;
            }

            GCHelper.CleanUp();

#if DEBUG
            // debug.ReportLiveDeviceObjects(ReportingLevel.Detail);
            // debug.Dispose();
            Debug.WriteLine(ObjectTable.ReportLeaks());
#endif
        }

        [NotNull]
        protected IDisposable ReplaceRenderView([CanBeNull] RenderTargetView view) {
            if (ReferenceEquals(_renderView, view) || view == null) return DisposeHelper.Empty;

            var renderView = _renderView;
            _renderView = view;
            return new ActionAsDisposable(() => _renderView = renderView);
        }

        private TargetResourceTexture _shotRenderBuffer, _shotMsaaTemporaryTexture, _shotDownsampleTexture, _shotCutTexture;

        protected int ShotInProcessValue { get; set; }
        public bool ShotInProcess => ShotInProcessValue > 0;
        public double ShotResolutionMultiplier { get; private set; }
        public bool ShotDrawInProcess { get; private set; }
        public bool ShotKeepHdr { get; private set; }

        protected virtual void DrawShot([CanBeNull] RenderTargetView target, [CanBeNull] IProgress<double> progress, CancellationToken cancellation) {
            try {
                ShotDrawInProcess = true;
                using (ReplaceRenderView(target)) {
                    DrawInner();
                }
            } finally {
                ShotDrawInProcess = false;
            }
        }

        protected virtual bool CanShotWithoutExtraTextures => !UseMsaa;
        protected int LastShotWidth, LastShotHeight;

        public void Shot(int baseWidth, int baseHeight, double downscale, double crop, Stream outputStream, RendererShotFormat shotFormat, IProgress<double> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            // AcToolsLogging.Write($"{baseWidth}×{baseHeight}, downscale={downscale}, crop={crop}");

            ShotInProcessValue++;
            var original = new { Width, Height, ResolutionMultiplier };

            try {
                Width = baseWidth;
                Height = baseHeight;
                ResolutionMultiplier = 1d;
                ShotResolutionMultiplier = (double)Width / original.Width;
                ShotKeepHdr = shotFormat.IsHdr();

                if (Equals(downscale, 1d) && Equals(crop, 1d) && CanShotWithoutExtraTextures && !ShotKeepHdr) {
                    // Simplest case: existing buffer will do just great, so let’s use it
                    if (_resized) {
                        Resize();
                        _resized = false;
                    }

                    DrawShot(null, progress.SubrangeDouble(0.05, 0.9), cancellation);
                    Texture2D.ToStream(DeviceContext, _renderBuffer, shotFormat.ToImageFileFormat(), outputStream);

                    var desc = _renderBuffer.Description;
                    LastShotWidth = desc.Width;
                    LastShotHeight = desc.Height;
                } else {
                    // More complicated situation: we need to temporary replace existing _renderBuffer
                    // with a custom one

                    var shotRenderBufferFormat = shotFormat.IsHdr() ? Format.R32G32B32A32_Float : Format.R8G8B8A8_UNorm;

                    // Preparing temporary replacement for _renderBuffer…
                    if (_shotRenderBuffer?.Format != shotRenderBufferFormat) {
                        // Let’s keep all those buffers in memory between shots to make series shooting faster
                        DisposeHelper.Dispose(ref _shotRenderBuffer);
                        _shotRenderBuffer = TargetResourceTexture.Create(shotRenderBufferFormat);
                    }

                    _shotRenderBuffer.Resize(DeviceContextHolder, Width, Height, SampleDescription);

                    // If we won’t call Resize() here, it will be called in Draw(), and then swap chain will
                    // be recreated in a wrong time
                    if (_resized) {
                        Resize();
                        _resized = false;
                    }

                    // Destroying current swap chain if needed
                    var swapChainMode = _swapChain != null;
                    if (swapChainMode) {
                        DisposeSwapChain();
                    }

                    // Calculating output width and height and, if needed, preparing downscaled buffer…
                    var outputWidth = (Width * downscale).RoundToInt();
                    var outputHeight = (Height * downscale).RoundToInt();

                    // Ready to draw!
                    DrawShot(_shotRenderBuffer.TargetView, progress.SubrangeDouble(0.05, 0.9), cancellation);

                    // For MSAA, we need to copy the result into a texture without MSAA enabled to save it later
                    TargetResourceTexture result;
                    if (UseMsaa) {
                        if (_shotMsaaTemporaryTexture?.Format != shotRenderBufferFormat) {
                            DisposeHelper.Dispose(ref _shotMsaaTemporaryTexture);
                            _shotMsaaTemporaryTexture = TargetResourceTexture.Create(shotRenderBufferFormat);
                        }

                        _shotMsaaTemporaryTexture.Resize(DeviceContextHolder, Width, Height, null);

                        DeviceContextHolder.GetHelper<CopyHelper>()
                                           .Draw(DeviceContextHolder, _shotRenderBuffer.View, _shotMsaaTemporaryTexture.TargetView);
                        result = _shotMsaaTemporaryTexture;
                    } else {
                        result = _shotRenderBuffer;
                    }

                    // Optional downscale
                    if (!Equals(downscale, 1d)) {
                        if (_shotDownsampleTexture?.Format != shotRenderBufferFormat) {
                            DisposeHelper.Dispose(ref _shotDownsampleTexture);
                            _shotDownsampleTexture = TargetResourceTexture.Create(shotRenderBufferFormat);
                        }

                        _shotDownsampleTexture.Resize(DeviceContextHolder, outputWidth, outputHeight, null);
                        DeviceContextHolder.GetHelper<DownsampleHelper>()
                                           .Draw(DeviceContextHolder, result, _shotDownsampleTexture);
                        result = _shotDownsampleTexture;
                    }

                    // Optional cut
                    if (!Equals(crop, 1d)) {
                        if (_shotCutTexture?.Format != shotRenderBufferFormat) {
                            DisposeHelper.Dispose(ref _shotCutTexture);
                            _shotCutTexture = TargetResourceTexture.Create(shotRenderBufferFormat);
                        }

                        var width = (outputWidth / crop).RoundToInt();
                        var height = (outputHeight / crop).RoundToInt();
                        DeviceContext.Rasterizer.SetViewports(new Viewport(0, 0, width, height));
                        _shotCutTexture.Resize(DeviceContextHolder, width, height, null);
                        DeviceContextHolder.GetHelper<CopyHelper>()
                                           .Cut(DeviceContextHolder, result.View, _shotCutTexture.TargetView, (float)crop);
                        result = _shotCutTexture;
                    }

                    var desc = result.Texture.Description;
                    LastShotWidth = desc.Width;
                    LastShotHeight = desc.Height;
                    shotFormat.ConvertToStream(DeviceContext, result.Texture, outputStream);

                    if (swapChainMode) {
                        RecreateSwapChain();
                    }
                }
            } catch (Exception e) {
                AcToolsLogging.Write("Error: " + e);
                throw;
            } finally {
                Width = original.Width;
                Height = original.Height;
                ResolutionMultiplier = original.ResolutionMultiplier;
                ShotKeepHdr = false;

                if (_resized) {
                    Resize();
                    _resized = false;
                }

                ShotInProcessValue--;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            if (Disposed) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
