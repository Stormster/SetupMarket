﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Debug = System.Diagnostics.Debug;
using Device = SlimDX.Direct3D11.Device;
using MapFlags = SlimDX.Direct3D11.MapFlags;

namespace AcTools.Render.Base {
    public class DeviceContextHolder : IDeviceContextHolder, IDisposable {
        public Device Device { get; }

        public float TimeFactor { get; set; }

        public DeviceContext DeviceContext { get; }

        private QuadBuffers _quadBuffers;

        public QuadBuffers QuadBuffers => _quadBuffers ?? (_quadBuffers = new QuadBuffers(Device));

        public void PrepareQuad(InputLayout layout) {
            QuadBuffers.Prepare(DeviceContext, layout);
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public SampleDescription SampleDescription { get; private set; }

        public DeviceContextHolder(Device device) {
            Device = device;
            DeviceContext = device.ImmediateContext;
        }

        public void OnResize(int width, int height, SampleDescription sampleDescription) {
            Width = width;
            Height = height;
            SampleDescription = sampleDescription;

            foreach (var helper in _helpers.Values) {
                helper.OnResize(this);
            }

            foreach (var effect in _effects.Values.OfType<IEffectScreenSizeWrapper>()) {
                effect.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));
            }
        }

        public long GetDedicatedVideoMemory() {
            using (var dxgiDevice = new SlimDX.DXGI.Device(Device))
            using (var adapter = dxgiDevice.Adapter){
                return adapter.Description.DedicatedVideoMemory;
            }
        }

        private readonly Dictionary<Type, IEffectWrapper> _effects = new Dictionary<Type, IEffectWrapper>();

        /// <summary>
        /// If you get effect this way, don’t call its Dispose()! It’ll be
        /// called automatically!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [NotNull]
        public T GetEffect<T>() where T : IEffectWrapper, new() {
            return GetEffect<T>(null);
        }

        /// <summary>
        /// If you get effect this way, don’t call its Dispose()! It’ll be
        /// called automatically!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [NotNull]
        public T GetEffect<T>([CanBeNull] Action<T> customPreInitialization) where T : IEffectWrapper, new() {
            var type = typeof(T);
            if (_effects.TryGetValue(type, out var result)) return (T)result;

            var created = (T)(_effects[type] = new T());
            customPreInitialization?.Invoke(created);
            created.Initialize(Device);
            return created;
        }

        /// <summary>
        /// If you get effect this way, don’t call its Dispose()! It’ll be
        /// called automatically!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [CanBeNull]
        public T GetExistingEffect<T>() where T : class, IEffectWrapper {
            return _effects.TryGetValue(typeof(T), out var result) ? (T)result : null;
        }

        private readonly Dictionary<Type, object> _something = new Dictionary<Type, object>();

        public void Set<T>(T obj) where T : class {
            _something[typeof(T)] = obj;
        }

        public void Remove<T>() where T : class {
            _something.Remove(typeof(T));
        }

        public T Get<T>() where T : class {
            var result = TryToGet<T>();
            if (result == null) throw new Exception($"Entry with type {typeof(T)} not found");

            return result;
        }

        public T TryToGet<T>() where T : class {
            var key = typeof(T);
            if (_something.TryGetValue(key, out var result)) {
                return (T)result;
            }

            if (typeof(T) == typeof(SharedMaterials)) {
                return GetSharedMaterialsInstance() as T;
            }

            T child = null;
            foreach (var o in _something) {
                child = o.Value as T;
                if (child != null) {
                    break;
                }
            }

            if (child != null) {
                _something[key] = child;
                return child;
            }

            return null;
        }

        private SharedMaterials _sharedMaterials;

        private SharedMaterials GetSharedMaterialsInstance() {
            return _sharedMaterials ?? (_sharedMaterials = new SharedMaterials(Get<IMaterialsFactory>()));
        }

        public IRenderableMaterial GetMaterial(object key) {
            return GetSharedMaterialsInstance().GetMaterial(key);
        }

        private readonly Dictionary<Type, IRenderHelper> _helpers = new Dictionary<Type, IRenderHelper>();

        /// <summary>
        /// If you get helper this way, don’t call its Dispose()! It’ll be
        /// called automatically!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [NotNull]
        public T GetHelper<T>() where T : IRenderHelper, new() {
            var type = typeof(T);
            if (_helpers.TryGetValue(type, out var result)) return (T)result;

            var created = (T)(_helpers[type] = new T());
            created.OnInitialize(this);
            if (Width != 0 || Height != 0) {
                created.OnResize(this);
            }
            return created;
        }

        [Pure]
        public IDisposable SaveRenderTargetAndViewport() {
            var targets = DeviceContext.OutputMerger.GetRenderTargets(2);
            var viewports = DeviceContext.Rasterizer.GetViewports();
            return new ActionAsDisposable(() => {
                DeviceContext.Rasterizer.SetViewports(viewports);
                DeviceContext.OutputMerger.SetTargets(targets);

                // TODO: WHAT IS GOING ON HERE?
                // targets.DisposeEverything();
            });
        }

        public event EventHandler UpdateRequired;
        public event EventHandler SceneUpdated;
        public event EventHandler TexturesUpdated;

        public void RaiseUpdateRequired() {
            UpdateRequired?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseSceneUpdated() {
            UpdateRequired?.Invoke(this, EventArgs.Empty);
            SceneUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseTexturesUpdated() {
            UpdateRequired?.Invoke(this, EventArgs.Empty);
            TexturesUpdated?.Invoke(this, EventArgs.Empty);
        }

        private CommonStates _states;

        public CommonStates States => _states ?? (_states = new CommonStates(Device));

        private readonly Dictionary<Tuple<int, int>, ShaderResourceView> _randomTextures =
                new Dictionary<Tuple<int, int>, ShaderResourceView>();

        public ShaderResourceView GetRandomTexture(int width, int height) {
            var size = Tuple.Create(width, height);
            if (!_randomTextures.TryGetValue(size, out var texture)) {
                var s = Stopwatch.StartNew();
                var r = new Random((width * 397) ^ height);
                texture = CreateTextureView(width, height, (x, y) => Color.FromArgb((int)(r.NextDouble() * ((double)int.MaxValue - int.MinValue) + int.MinValue)));
                _randomTextures[size] = texture;
                AcToolsLogging.Write($"{width}×{height}: {s.Elapsed.TotalMilliseconds:F1} ms");
            }

            return texture;
        }

        private ShaderResourceView _flatNmView, _transparentView;

        public delegate Color FillColor(int x, int y);

        [NotNull]
        public Texture2D CreateTexture(int width, int height, FillColor fill) {
            var texture = new Texture2D(Device, new Texture2DDescription {
                SampleDescription = new SampleDescription(1, 0),
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm,
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.Write
            });

            var rect = DeviceContext.MapSubresource(texture, 0, MapMode.WriteDiscard, MapFlags.None);
            if (rect.Data.CanWrite) {
                for (var y = 0; y < width; y++) {
                    var rowStart = y * rect.RowPitch;
                    rect.Data.Seek(rowStart, System.IO.SeekOrigin.Begin);
                    for (var x = 0; x < width; x++) {
                        var c = fill.Invoke(x, y);
                        rect.Data.WriteByte(c.R);
                        rect.Data.WriteByte(c.G);
                        rect.Data.WriteByte(c.B);
                        rect.Data.WriteByte(c.A);
                    }
                }
            }

            DeviceContext.UnmapSubresource(texture, 0);
            return texture;
        }

        [NotNull]
        public ShaderResourceView CreateTextureView(int width, int height, FillColor fill) {
            using (var texture = CreateTexture(width, height, fill)) {
                return new ShaderResourceView(Device, texture, new ShaderResourceViewDescription {
                    Format = texture.Description.Format,
                    Dimension = ShaderResourceViewDimension.Texture2D,
                    MostDetailedMip = 0,
                    MipLevels = 1
                });
            }
        }

        public delegate Color4 FillColor4(int x, int y);

        [NotNull]
        public Texture2D CreateTexture(int width, int height, FillColor4 fill) {
            var texture = new Texture2D(Device, new Texture2DDescription {
                SampleDescription = new SampleDescription(1, 0),
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R32G32B32A32_Float,
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.Write
            });

            var rect = DeviceContext.MapSubresource(texture, 0, MapMode.WriteDiscard, MapFlags.None);
            if (rect.Data.CanWrite) {
                for (var y = 0; y < width; y++) {
                    var rowStart = y * rect.RowPitch;
                    rect.Data.Seek(rowStart, System.IO.SeekOrigin.Begin);
                    for (var x = 0; x < width; x++) {
                        var c = fill.Invoke(x, y);
                        rect.Data.Write(c.Red);
                        rect.Data.Write(c.Green);
                        rect.Data.Write(c.Blue);
                        rect.Data.Write(c.Alpha);
                    }
                }
            }

            DeviceContext.UnmapSubresource(texture, 0);
            return texture;
        }

        [NotNull]
        public ShaderResourceView CreateTextureView(int width, int height, FillColor4 fill) {
            using (var texture = CreateTexture(width, height, fill)) {
                return new ShaderResourceView(Device, texture, new ShaderResourceViewDescription {
                    Format = texture.Description.Format,
                    Dimension = ShaderResourceViewDimension.Texture2D,
                    MostDetailedMip = 0,
                    MipLevels = 1
                });
            }
        }

        public ShaderResourceView GetFlatNmTexture() {
            return _flatNmView ?? (_flatNmView = CreateTextureView(4, 4, (x, y) => Color.FromArgb(255, 127, 127, 255)));
        }

        public ShaderResourceView GetTransparentTexture() {
            return _transparentView ?? (_transparentView = CreateTextureView(4, 4, (x, y) => Color.FromArgb(0, 0, 0, 0)));
        }

        double IDeviceContextHolder.LastFrameTime => _clock.GetLastFrameTime();

        public void Dispose(SwapChain swapChain) {
            Debug.WriteLine("DeviceContextHolder.Dispose()");

            DisposeHelper.Dispose(ref _states);
            Debug.WriteLine("_states disposed");

            DisposeHelper.Dispose(ref _quadBuffers);
            Debug.WriteLine("_quadBuffers disposed");

            _effects.DisposeEverything();
            Debug.WriteLine("_effects disposed");

            _helpers.DisposeEverything();
            Debug.WriteLine("_helpers disposed");

            _randomTextures.DisposeEverything();
            Debug.WriteLine("_randomTextures disposed");

            DisposeHelper.Dispose(ref _flatNmView);
            Debug.WriteLine("_flatNmView disposed");

            DisposeHelper.Dispose(ref _transparentView);
            Debug.WriteLine("_transparentView disposed");

            _something.Values.OfType<IDisposable>().DisposeEverything();
            _something.Clear();
            Debug.WriteLine("_something disposed");

            DeviceContext.ClearState();
            DeviceContext.Flush();

            // DeviceContext.Dispose();
            // Debug.WriteLine("DeviceContext disposed");

            if (swapChain != null) {
                swapChain.Dispose();
                Debug.WriteLine("SwapChain disposed");
            }

            Device.Dispose();
            Debug.WriteLine("Device disposed");
            
            // Apparently destroying one of DeviceContext and Device destroys another?
        }

        public void Dispose() {
            Dispose(null);
        }

        private readonly RendererClock _clock = new RendererClock(32);

        public RendererStopwatch StartNewStopwatch() {
            return new RendererStopwatch(_clock);
        }

        public void OnTick(float dt) {
            _clock.RegisterFrame(dt);
        }
    }
}
