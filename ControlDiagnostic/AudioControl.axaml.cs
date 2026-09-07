using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Black_Hole_Cross.Services;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.EXT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Black_Hole_Cross.ControlDiagnostic
{
    public unsafe partial class AudioControl : UserControl
    {
        private ALContext? _alc;
        private AL? _al;
        private Capture? _captureApi;
        private Device* _captureDevice = null;
        private Device* _outputDevice = null;
        private Context* _context = null;
        private uint _sourceId;

        private const int NumBuffers = 4;
        private uint[] _buffers = new uint[NumBuffers];
        private Queue<uint> _freeBuffers = new Queue<uint>();

        private DispatcherTimer? _uiTimer;
        private bool _isInitialized = false;
        private bool _isLoopbackActive = false;
        private bool _isNoiseSuppression = false;
        private bool _isCompressorActive = false;
        private int _selectedPreset = 0;

        private short[] _audioBuffer = new short[4096];
        private float[] _echoBuffer = new float[44100];
        private int _echoWritePos = 0;

        private int _sampleRate = 44100;
        private float _masterGain = 1.8f;
        private float _wetMix = 0.7f;
        private float _ringPhase = 0.0f;
        private float _inputGain = 1.8f;

        private float _hpState = 0f;
        private float _lpState = 0f;
        private float[] _reverbBuffer = new float[22050];
        private int _reverbWritePos = 0;
        private float _vibratoPhase = 0f;

        private enum VoicePreset
        {
            Clean = 0,
            Demon = 1,
            Robot = 2,
            Chipmunk = 3,
            Helium = 4,
            Alien = 5,
            Telephone = 6,
            Radio = 7,
            EchoSpace = 8,
            DeepSub = 9,
            AutotuneTrap = 10,
            Megaphone = 11,
            WhisperGhost = 12,
            Angel = 13,
            Thunder = 14,
            BassBoost = 15,
            StereoWide = 16,
            Vinyl = 17,
            Giant = 18,
            RadioStatic = 19,
            Underwater = 20,
            Distorted = 21,
            SciFi = 22,
            PitchGlitch = 23,
            PhaseShifter = 24,
            CombFilter = 25,
            SpectralFrost = 26
        }

        static AudioControl()
        {
            LoadOpenALLibrary();
        }

        private static void LoadOpenALLibrary()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dllPath = Path.Combine(baseDir, "OpenAL32.dll");

                if (File.Exists(dllPath))
                {
                    if (NativeLibrary.TryLoad(dllPath, out IntPtr handle))
                    {
                        NativeLibrary.SetDllImportResolver(typeof(ALContext).Assembly, (name, assembly, searchPath) =>
                        {
                            if (name.Contains("openal", StringComparison.OrdinalIgnoreCase)) return handle;
                            return IntPtr.Zero;
                        });
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"❌ OpenAL Load Error: {ex.Message}"); }
        }

        public AudioControl()
        {
            InitializeComponent();
            Loaded += AudioControl_Loaded;
            Unloaded += AudioControl_Unloaded;

            DiagnosAudio.Text = LocalizationService.Instance["AudioDiagnostics"] ?? "Аудио студио";
            descrip.Text = LocalizationService.Instance["AudioDesc"] ?? "Профессиональный вокальный процессор и анализатор спектра";

            InitVoicePresets();
        }

        private void InitVoicePresets()
        {
            VoicePresetCombo.ItemsSource = new List<string>
            {
                "🎙️ Clean (Без обработки)",
                "👹 Demon (Мрачный баритон)",
                "🤖 Cyborg (Робот / Vocoder)",
                "🐿️ Chipmunk (Бурундук)",
                "🎈 Helium (Гелиевый питч)",
                "👽 Alien (Фланжер)",
                "📞 Telephone (Телефон)",
                "📻 Vintage Radio (Радио)",
                "🌌 Echo Space (Эхо)",
                "🔊 Deep Sub Bass (Суб-октава)",
                "🎤 Auto-Trap (Трэп)",
                "📢 Megaphone (Мегафон)",
                "👻 Whisper Ghost (Шёпот)",
                "😇 Angel (Небесный)",
                "⛈️ Thunder (Гром)",
                "🎚️ Bass Boost (Басс)",
                "🌊 Stereo Wide (Стерео)",
                "🎵 Vinyl (Винил)",
                "🏔️ Giant (Великан)",
                "📡 Radio Static (Радиопомехи)",
                "🌊 Underwater (Под водой)",
                "💢 Distorted (Биткраш)",
                "🚀 Sci-Fi (Фантастика)",
                "💥 Pitch Glitch (Глитч-питч)",
                "🌀 Phase Shifter (Фазовый вращатель)",
                "🔱 Comb Filter (Гребенчатый фильтр)",
                "❄️ Spectral Frost (Спектральный мороз)"
            };
            VoicePresetCombo.SelectedIndex = 0;
        }

        private void AudioControl_Loaded(object? sender, RoutedEventArgs e)
        {
            InitOpenALCapture();
            InitOpenALOutput();

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            _uiTimer.Tick += UpdateAudioMetrics;
            _uiTimer.Start();
        }

        private void AudioControl_Unloaded(object? sender, RoutedEventArgs e)
        {
            _uiTimer?.Stop();
            CleanupOpenAL();
        }

        private void InitOpenALCapture()
        {
            try
            {
                _alc = ALContext.GetApi(true);
                if (_alc == null) return;
                if (!_alc.TryGetExtension<Capture>((Device*)null, out _captureApi)) return;

                _captureDevice = _captureApi.CaptureOpenDevice(null, 44100, BufferFormat.Mono16, 4410);
                if (_captureDevice == null) return;

                _captureApi.CaptureStart(_captureDevice);
                RecordDeviceText.Text = "🎤 MIC INPUT (Active)";
            }
            catch (Exception ex) { Debug.WriteLine($"❌ Capture Error: {ex}"); }
        }

        private void InitOpenALOutput()
        {
            try
            {
                _outputDevice = _alc!.OpenDevice(null);
                if (_outputDevice == null) return;

                _context = _alc.CreateContext(_outputDevice, null);
                if (_context == null) return;

                _alc.MakeContextCurrent(_context);
                _al = AL.GetApi(true);
                if (_al == null) return;

                fixed (uint* bPtr = _buffers) { _al.GenBuffers(NumBuffers, bPtr); }

                uint src = 0;
                _al.GenSources(1, &src);
                _sourceId = src;

                _freeBuffers.Clear();
                for (int i = 0; i < NumBuffers; i++) _freeBuffers.Enqueue(_buffers[i]);

                _al.SetSourceProperty(_sourceId, SourceFloat.Gain, _masterGain);
                PlaybackDeviceText.Text = "🔊 MASTER OUT (OpenAL)";
                _isInitialized = true;
            }
            catch (Exception ex) { Debug.WriteLine($"❌ Output Error: {ex}"); }
        }

        private void ProcessAudioData(short[] samples, int count)
        {
            if (!_isLoopbackActive || _al == null || _sourceId == 0) return;

            try
            {
                // Усиление входа
                for (int i = 0; i < count; i++)
                {
                    samples[i] = (short)Math.Clamp(samples[i] * _inputGain, -32768, 32767);
                }

                short[] dryBuffer = new short[count];
                Array.Copy(samples, dryBuffer, count);

                ApplyEffects(samples, count);

                for (int i = 0; i < count; i++)
                {
                    float mixed = (dryBuffer[i] * (1.0f - _wetMix)) + (samples[i] * _wetMix);
                    samples[i] = (short)Math.Clamp(mixed, -32768, 32767);
                }

                // Финальное усиление
                for (int i = 0; i < count; i++)
                {
                    samples[i] = (short)Math.Clamp(samples[i] * 1.4f, -32768, 32767);
                }

                int processed = 0;
                _al.GetSourceProperty(_sourceId, GetSourceInteger.BuffersProcessed, out processed);
                while (processed > 0)
                {
                    uint unqueuedBuffer = 0;
                    _al.SourceUnqueueBuffers(_sourceId, 1, &unqueuedBuffer);
                    _freeBuffers.Enqueue(unqueuedBuffer);
                    processed--;
                }

                if (_freeBuffers.Count > 0)
                {
                    uint bufId = _freeBuffers.Dequeue();
                    byte[] byteBuffer = new byte[count * 2];
                    Buffer.BlockCopy(samples, 0, byteBuffer, 0, count * 2);

                    fixed (byte* ptr = byteBuffer)
                    {
                        _al.BufferData(bufId, BufferFormat.Mono16, ptr, count * 2, _sampleRate);
                    }

                    _al.SourceQueueBuffers(_sourceId, 1, &bufId);

                    _al.GetSourceProperty(_sourceId, GetSourceInteger.SourceState, out int state);
                    if ((SourceState)state != SourceState.Playing)
                    {
                        _al.SourcePlay(_sourceId);
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"❌ Process Error: {ex.Message}"); }
        }

        private void ApplyEffects(short[] samples, int count)
        {
            if (_isNoiseSuppression)
            {
                ApplyImprovedNoiseGate(samples, count);
            }

            switch ((VoicePreset)_selectedPreset)
            {
                case VoicePreset.Demon: ApplyDemon(samples, count); break;
                case VoicePreset.Robot: ApplyRobot(samples, count); break;
                case VoicePreset.Chipmunk: ApplyChipmunk(samples, count); break;
                case VoicePreset.Helium: ApplyHelium(samples, count); break;
                case VoicePreset.Alien: ApplyAlienFlanger(samples, count); break;
                case VoicePreset.Telephone: ApplyTelephoneBandpass(samples, count); break;
                case VoicePreset.Radio: ApplyRadio(samples, count); break;
                case VoicePreset.EchoSpace: ApplyEchoSpace(samples, count); break;
                case VoicePreset.DeepSub: ApplyDeepSub(samples, count); break;
                case VoicePreset.AutotuneTrap: ApplyAutotuneTrap(samples, count); break;
                case VoicePreset.Megaphone: ApplyMegaphone(samples, count); break;
                case VoicePreset.WhisperGhost: ApplyWhisperGhost(samples, count); break;
                case VoicePreset.Angel: ApplyAngel(samples, count); break;
                case VoicePreset.Thunder: ApplyThunder(samples, count); break;
                case VoicePreset.BassBoost: ApplyBassBoost(samples, count); break;
                case VoicePreset.StereoWide: ApplyStereoWide(samples, count); break;
                case VoicePreset.Vinyl: ApplyVinyl(samples, count); break;
                case VoicePreset.Giant: ApplyGiant(samples, count); break;
                case VoicePreset.RadioStatic: ApplyRadioStatic(samples, count); break;
                case VoicePreset.Underwater: ApplyUnderwater(samples, count); break;
                case VoicePreset.Distorted: ApplyDistorted(samples, count); break;
                case VoicePreset.SciFi: ApplySciFi(samples, count); break;
                case VoicePreset.PitchGlitch: ApplyPitchGlitch(samples, count); break;
                case VoicePreset.PhaseShifter: ApplyPhaseShifter(samples, count); break;
                case VoicePreset.CombFilter: ApplyCombFilter(samples, count); break;
                case VoicePreset.SpectralFrost: ApplySpectralFrost(samples, count); break;
            }

            if (_isCompressorActive)
            {
                ApplyImprovedCompressor(samples, count);
            }
        }

      

        private void ApplyImprovedNoiseGate(short[] samples, int count)
        {
            float threshold = (float)GateSlider.Value * 12f;
            float smoothFactor = 0.02f;
            float lastGain = 1.0f;

            for (int i = 0; i < count; i++)
            {
                float abs = MathF.Abs(samples[i]);
                float targetGain = abs > threshold ? 1.0f : abs / (threshold + 0.001f);
                targetGain = MathF.Pow(targetGain, 1.5f);
                lastGain = lastGain + (targetGain - lastGain) * smoothFactor;
                samples[i] = (short)(samples[i] * lastGain);
            }
        }

        private void ApplyImprovedCompressor(short[] samples, int count)
        {
            float threshold = 12000f;
            float ratio = 3.0f;
            float attack = 0.1f;
            float release = 0.01f;
            float envelope = 1.0f;

            for (int i = 0; i < count; i++)
            {
                float abs = MathF.Abs(samples[i]);
                float gain = abs > threshold ? 1.0f - (abs - threshold) / (abs * ratio) : 1.0f;
                float attackRelease = gain > envelope ? attack : release;
                envelope = envelope + (gain - envelope) * attackRelease;
                samples[i] = (short)(samples[i] * envelope);
            }
        }

      

        private void ApplyDemon(short[] samples, int count)
        {
            // Питч шифт вниз
            float pitchRatio = 0.72f;
            short[] output = new short[count];
            float readPos = 0;

            for (int i = 0; i < count; i++)
            {
                int idx0 = (int)readPos;
                int idx1 = Math.Min(idx0 + 1, count - 1);
                float frac = readPos - idx0;
                float sample = samples[idx0] * (1.0f - frac) + samples[idx1] * frac;
                output[i] = (short)Math.Clamp(sample, -32768, 32767);
                readPos += pitchRatio;
                if (readPos >= count) readPos -= count;
            }

            for (int i = 0; i < count; i++)
            {
                float s = output[i] / 32768f;
                s = MathF.Tanh(s * 1.8f);
                samples[i] = (short)Math.Clamp(s * 30000f, -32768, 32767);
            }
        }

        private void ApplyRobot(short[] samples, int count)
        {
            float carrierFreq = 55.0f;
            float phase = 0;

            for (int i = 0; i < count; i++)
            {
                phase += 2.0f * MathF.PI * carrierFreq / _sampleRate;
                if (phase > 2.0f * MathF.PI) phase -= 2.0f * MathF.PI;
                float carrier = MathF.Sin(phase) * 0.8f + MathF.Sin(phase * 2.0f) * 0.2f;
                float robotSample = samples[i] * carrier;
                robotSample = MathF.Tanh(robotSample / 20000f) * 28000f;
                samples[i] = (short)Math.Clamp(robotSample, -32768, 32767);
            }
        }

        private void ApplyChipmunk(short[] samples, int count)
        {
            float pitchRatio = 1.35f;
            short[] output = new short[count];
            float readPos = 0;

            for (int i = 0; i < count; i++)
            {
                int idx0 = (int)readPos;
                int idx1 = Math.Min(idx0 + 1, count - 1);
                float frac = readPos - idx0;
                float sample = samples[idx0] * (1.0f - frac) + samples[idx1] * frac;
                output[i] = (short)Math.Clamp(sample, -32768, 32767);
                readPos += pitchRatio;
                if (readPos >= count) readPos -= count;
            }

            for (int i = 0; i < count; i++)
            {
                float s = output[i] / 32768f;
                s = s * 1.1f;
                samples[i] = (short)Math.Clamp(s * 32768f, -32768, 32767);
            }
        }

        private void ApplyHelium(short[] samples, int count)
        {
            float pitchRatio = 1.6f;
            short[] output = new short[count];
            float readPos = 0;

            for (int i = 0; i < count; i++)
            {
                int idx0 = (int)readPos;
                int idx1 = Math.Min(idx0 + 1, count - 1);
                float frac = readPos - idx0;
                float sample = samples[idx0] * (1.0f - frac) + samples[idx1] * frac;
                output[i] = (short)Math.Clamp(sample, -32768, 32767);
                readPos += pitchRatio;
                if (readPos >= count) readPos -= count;
            }

            for (int i = 0; i < count; i++)
            {
                float s = output[i] / 32768f;
                s = s * 1.2f;
                samples[i] = (short)Math.Clamp(s * 32768f, -32768, 32767);
            }
        }

        private void ApplyAlienFlanger(short[] samples, int count)
        {
            float depth = 20.0f;
            float rate = 0.08f;
            float phase = 0;

            for (int i = 0; i < count; i++)
            {
                phase += rate;
                float sinMod = MathF.Sin(phase) * depth;
                int src = Math.Clamp(i + (int)sinMod, 0, count - 1);
                samples[i] = (short)Math.Clamp((samples[i] * 0.4f + samples[src] * 0.6f), -32768, 32767);
            }
        }

        private void ApplyTelephoneBandpass(short[] samples, int count)
        {
            float hpCoeff = 0.15f;
            float lpCoeff = 0.25f;
            float hp = 0f, lp = 0f;

            for (int i = 0; i < count; i++)
            {
                hp += (samples[i] - hp) * hpCoeff;
                float hpOut = samples[i] - hp;
                lp += (hpOut - lp) * lpCoeff;
                samples[i] = (short)Math.Clamp(lp * 2.0f, -32768, 32767);
            }
        }

        private void ApplyRadio(short[] samples, int count)
        {
            float hpCoeff = 0.15f;
            float lpCoeff = 0.25f;
            float hp = 0f, lp = 0f;

            for (int i = 0; i < count; i++)
            {
                hp += (samples[i] - hp) * hpCoeff;
                float hpOut = samples[i] - hp;
                lp += (hpOut - lp) * lpCoeff;
                samples[i] = (short)Math.Clamp(lp * 2.0f, -32768, 32767);
            }

            for (int i = 0; i < count; i++)
            {
                float s = samples[i] / 32768f;
                s = MathF.Tanh(s * 3.0f);
                samples[i] = (short)Math.Clamp(s * 20000f, -32768, 32767);
            }
        }

        private void ApplyEchoSpace(short[] samples, int count)
        {
            float feedback = 0.55f;      
            float damp = 0.65f;          
            int delaySamples = 11025;    

           
            for (int i = 0; i < count; i++)
            {
                
                int readPos = (_echoWritePos - delaySamples + _echoBuffer.Length) % _echoBuffer.Length;
                float echoSample = _echoBuffer[readPos] * damp;

               
                float outSample = samples[i] + echoSample * feedback;

                
                _echoBuffer[_echoWritePos] = outSample * 0.9f;
                _echoWritePos = (_echoWritePos + 1) % _echoBuffer.Length;

                
                samples[i] = (short)Math.Clamp(outSample, -32768, 32767);
            }
        }

        private void ApplySciFi(short[] samples, int count)
        {
           
            float depth = 20.0f;
            float rate = 0.08f;
            float phase = 0;

            for (int i = 0; i < count; i++)
            {
                phase += rate;
                float sinMod = MathF.Sin(phase) * depth;
                int src = Math.Clamp(i + (int)sinMod, 0, count - 1);
                samples[i] = (short)Math.Clamp((samples[i] * 0.4f + samples[src] * 0.6f), -32768, 32767);
            }

         
            float feedback = 0.4f;
            float damp = 0.6f;
            int delaySamples = 8820;

            for (int i = 0; i < count; i++)
            {
                int readPos = (_echoWritePos - delaySamples + _echoBuffer.Length) % _echoBuffer.Length;
                float echoSample = _echoBuffer[readPos] * damp;
                float outSample = samples[i] + echoSample * feedback;
                _echoBuffer[_echoWritePos] = outSample * 0.85f;
                _echoWritePos = (_echoWritePos + 1) % _echoBuffer.Length;
                samples[i] = (short)Math.Clamp(outSample, -32768, 32767);
            }

           
            float sweepPhase = 0;
            for (int i = 0; i < count; i++)
            {
                sweepPhase += 0.05f;
                float sweep = MathF.Sin(sweepPhase * 0.5f) * 0.2f + 0.8f;
                samples[i] = (short)Math.Clamp(samples[i] * sweep, -32768, 32767);
            }

           
            float pingPhase = 0;
            for (int i = 0; i < count; i++)
            {
                pingPhase += 0.005f;
                float ping = MathF.Sin(pingPhase * 10f) * 0.1f;
                samples[i] = (short)Math.Clamp(samples[i] + ping * 1000f, -32768, 32767);
            }
        }

        private void ApplyUnderwater(short[] samples, int count)
        {
          
            float lpCoeff = 0.08f;
            float lp = 0f;
            float phase = 0;

            for (int i = 0; i < count; i++)
            {
                lp += (samples[i] - lp) * lpCoeff;
                samples[i] = (short)Math.Clamp(lp, -32768, 32767);
            }

           
            for (int i = 0; i < count; i++)
            {
                phase += 0.03f;
                float mod = MathF.Sin(phase) * 0.15f + 0.85f;
                samples[i] = (short)Math.Clamp(samples[i] * mod, -32768, 32767);
            }

           
            float feedback = 0.5f;
            float damp = 0.5f;
            int delaySamples = 13230;

            for (int i = 0; i < count; i++)
            {
                int readPos = (_echoWritePos - delaySamples + _echoBuffer.Length) % _echoBuffer.Length;
                float echoSample = _echoBuffer[readPos] * damp;
                float outSample = samples[i] + echoSample * feedback;
                _echoBuffer[_echoWritePos] = outSample * 0.8f;
                _echoWritePos = (_echoWritePos + 1) % _echoBuffer.Length;
                samples[i] = (short)Math.Clamp(outSample, -32768, 32767);
            }
        }

        private void ApplyAngel(short[] samples, int count)
        {
            float pitchRatio = 1.3f;
            short[] output = new short[count];
            float readPos = 0;

            for (int i = 0; i < count; i++)
            {
                int idx0 = (int)readPos;
                int idx1 = Math.Min(idx0 + 1, count - 1);
                float frac = readPos - idx0;
                float sample = samples[idx0] * (1.0f - frac) + samples[idx1] * frac;
                output[i] = (short)Math.Clamp(sample, -32768, 32767);
                readPos += pitchRatio;
                if (readPos >= count) readPos -= count;
            }

          
            for (int i = 0; i < count; i++)
            {
                float s = output[i] / 32768f;
                s = s * 1.2f + MathF.Sin(s * 3.0f) * 0.1f;
                samples[i] = (short)Math.Clamp(s * 30000f, -32768, 32767);
            }

          
            float decay = 0.7f;
            float damp = 0.4f;
            int delaySamples = 11025;

            for (int i = 0; i < count; i++)
            {
                int readPos2 = (_reverbWritePos - delaySamples + _reverbBuffer.Length) % _reverbBuffer.Length;
                float reverbSample = _reverbBuffer[readPos2] * decay * damp;
                float outSample = samples[i] + reverbSample;
                _reverbBuffer[_reverbWritePos] = outSample * 0.7f;
                _reverbWritePos = (_reverbWritePos + 1) % _reverbBuffer.Length;
                samples[i] = (short)Math.Clamp(outSample, -32768, 32767);
            }
        }

        private void ApplyDeepSub(short[] samples, int count)
        {
            float pitchRatio = 0.7f;
            short[] output = new short[count];
            float readPos = 0;

            for (int i = 0; i < count; i++)
            {
                int idx0 = (int)readPos;
                int idx1 = Math.Min(idx0 + 1, count - 1);
                float frac = readPos - idx0;
                float sample = samples[idx0] * (1.0f - frac) + samples[idx1] * frac;
                output[i] = (short)Math.Clamp(sample, -32768, 32767);
                readPos += pitchRatio;
                if (readPos >= count) readPos -= count;
            }

            for (int i = 0; i < count; i++)
            {
                float s = output[i] / 32768f;
                s = MathF.Tanh(s * 1.3f);
                samples[i] = (short)Math.Clamp(s * 30000f, -32768, 32767);
            }
        }

        private void ApplyAutotuneTrap(short[] samples, int count)
        {
            float pitchRatio = 1.12f;
            short[] output = new short[count];
            float readPos = 0;
            float phase = 0;

            for (int i = 0; i < count; i++)
            {
                int idx0 = (int)readPos;
                int idx1 = Math.Min(idx0 + 1, count - 1);
                float frac = readPos - idx0;
                float sample = samples[idx0] * (1.0f - frac) + samples[idx1] * frac;
                output[i] = (short)Math.Clamp(sample, -32768, 32767);
                readPos += pitchRatio;
                if (readPos >= count) readPos -= count;
            }

            for (int i = 0; i < count; i++)
            {
                phase += 0.08f;
                float mod = MathF.Sin(phase) * 0.15f + 1.0f;
                samples[i] = (short)Math.Clamp(output[i] * mod, -32768, 32767);
            }
        }

        private void ApplyMegaphone(short[] samples, int count)
        {
            float hpCoeff = 0.15f;
            float lpCoeff = 0.25f;
            float hp = 0f, lp = 0f;

            for (int i = 0; i < count; i++)
            {
                hp += (samples[i] - hp) * hpCoeff;
                float hpOut = samples[i] - hp;
                lp += (hpOut - lp) * lpCoeff;
                samples[i] = (short)Math.Clamp(lp * 2.0f, -32768, 32767);
            }

            for (int i = 0; i < count; i++)
            {
                float s = samples[i] / 32768f;
                s = MathF.Tanh(s * 2.0f);
                samples[i] = (short)Math.Clamp(s * 25000f, -32768, 32767);
            }
        }

        private void ApplyWhisperGhost(short[] samples, int count)
        {
            float hpState = 0f;

            for (int i = 0; i < count; i++)
            {
                float absSample = MathF.Abs(samples[i]);
                float noise = (Random.Shared.NextSingle() * 2.0f - 1.0f) * absSample * 0.7f;
                hpState += (noise - hpState) * 0.35f;
                samples[i] = (short)Math.Clamp(noise - hpState, -32768, 32767);
            }
        }

        

        private void ApplyThunder(short[] samples, int count)
        {
            float pitchRatio = 0.55f;
            short[] output = new short[count];
            float readPos = 0;

            for (int i = 0; i < count; i++)
            {
                int idx0 = (int)readPos;
                int idx1 = Math.Min(idx0 + 1, count - 1);
                float frac = readPos - idx0;
                float sample = samples[idx0] * (1.0f - frac) + samples[idx1] * frac;
                output[i] = (short)Math.Clamp(sample, -32768, 32767);
                readPos += pitchRatio;
                if (readPos >= count) readPos -= count;
            }

            for (int i = 0; i < count; i++)
            {
                float s = output[i] / 32768f;
                s = MathF.Tanh(s * 2.5f);
                s = s * 1.4f;
                samples[i] = (short)Math.Clamp(s * 30000f, -32768, 32767);
            }

          
            float[] bassBuffer = new float[count];
            for (int i = 0; i < count; i++)
            {
                float sample = samples[i] / 32768f;
                bassBuffer[i] = i > 0 ? bassBuffer[i - 1] + (sample - bassBuffer[i - 1]) * 0.15f : sample;
                float boosted = sample + bassBuffer[i] * 2.0f;
                samples[i] = (short)Math.Clamp(boosted * 32768f, -32768, 32767);
            }
        }

        private void ApplyBassBoost(short[] samples, int count)
        {
            float[] bassBuffer = new float[count];

            for (int i = 0; i < count; i++)
            {
                float sample = samples[i] / 32768f;
                bassBuffer[i] = i > 0 ? bassBuffer[i - 1] + (sample - bassBuffer[i - 1]) * 0.15f : sample;
                float boosted = sample + bassBuffer[i] * 2.0f;
                samples[i] = (short)Math.Clamp(boosted * 32768f, -32768, 32767);
            }
        }

        private void ApplyStereoWide(short[] samples, int count)
        {
            int delay = 15;

            for (int i = 0; i < count; i++)
            {
                float sample = samples[i] / 32768f;
                int delayIdx = Math.Max(0, i - delay);
                float wide = (sample + samples[delayIdx] / 32768f) * 0.7f;
                samples[i] = (short)Math.Clamp(wide * 32768f, -32768, 32767);
            }
        }

        private void ApplyVinyl(short[] samples, int count)
        {
            float hpCoeff = 0.15f;
            float lpCoeff = 0.25f;
            float hp = 0f, lp = 0f;

            for (int i = 0; i < count; i++)
            {
                hp += (samples[i] - hp) * hpCoeff;
                float hpOut = samples[i] - hp;
                lp += (hpOut - lp) * lpCoeff;
                samples[i] = (short)Math.Clamp(lp * 2.0f, -32768, 32767);
            }

            for (int i = 0; i < count; i++)
            {
                float s = samples[i] / 32768f;
                s = s * 0.9f + MathF.Sin(s * 2.0f) * 0.05f;
                float noise = (Random.Shared.NextSingle() - 0.5f) * 0.02f;
                samples[i] = (short)Math.Clamp((s + noise) * 30000f, -32768, 32767);
            }
        }

        private void ApplyGiant(short[] samples, int count)
        {
            float pitchRatio = 0.45f;
            short[] output = new short[count];
            float readPos = 0;

            for (int i = 0; i < count; i++)
            {
                int idx0 = (int)readPos;
                int idx1 = Math.Min(idx0 + 1, count - 1);
                float frac = readPos - idx0;
                float sample = samples[idx0] * (1.0f - frac) + samples[idx1] * frac;
                output[i] = (short)Math.Clamp(sample, -32768, 32767);
                readPos += pitchRatio;
                if (readPos >= count) readPos -= count;
            }

          
            float[] bassBuffer = new float[count];
            for (int i = 0; i < count; i++)
            {
                float sample = output[i] / 32768f;
                bassBuffer[i] = i > 0 ? bassBuffer[i - 1] + (sample - bassBuffer[i - 1]) * 0.15f : sample;
                float boosted = sample + bassBuffer[i] * 2.0f;
                output[i] = (short)Math.Clamp(boosted * 32768f, -32768, 32767);
            }

            for (int i = 0; i < count; i++)
            {
                float s = output[i] / 32768f;
                s = MathF.Tanh(s * 1.5f);
                samples[i] = (short)Math.Clamp(s * 32000f, -32768, 32767);
            }
        }

        private void ApplyRadioStatic(short[] samples, int count)
        {
            float hpCoeff = 0.15f;
            float lpCoeff = 0.25f;
            float hp = 0f, lp = 0f;

            for (int i = 0; i < count; i++)
            {
                hp += (samples[i] - hp) * hpCoeff;
                float hpOut = samples[i] - hp;
                lp += (hpOut - lp) * lpCoeff;
                samples[i] = (short)Math.Clamp(lp * 2.0f, -32768, 32767);
            }

            for (int i = 0; i < count; i++)
            {
                float noise = (Random.Shared.NextSingle() - 0.5f) * 0.15f;
                float s = samples[i] / 32768f;
                float crackle = (Random.Shared.NextSingle() > 0.98f) ? (Random.Shared.NextSingle() - 0.5f) * 0.5f : 0f;
                samples[i] = (short)Math.Clamp((s + noise + crackle) * 28000f, -32768, 32767);
            }
        }

       

        private void ApplyDistorted(short[] samples, int count)
        {
            int bits = 6;
            int step = 32768 / (1 << bits);

            for (int i = 0; i < count; i++)
            {
                int quantized = (samples[i] / step) * step;
                samples[i] = (short)Math.Clamp(quantized, -32768, 32767);
            }

            for (int i = 0; i < count; i++)
            {
                float s = samples[i] / 32768f;
                s = MathF.Tanh(s * 4.0f);
                samples[i] = (short)Math.Clamp(s * 30000f, -32768, 32767);
            }
        }

       

        private void ApplyPitchGlitch(short[] samples, int count)
        {
            short[] output = new short[count];

            for (int i = 0; i < count; i++)
            {
                float glitchAmount = (MathF.Sin(i * 0.02f * 10f) * 0.5f + 0.5f) * 2.0f;
                glitchAmount = MathF.Pow(glitchAmount, 2f) + 0.5f;
                int idx = (int)(i * glitchAmount) % count;
                output[i] = samples[idx];
            }

            for (int i = 0; i < count; i++)
            {
                float noise = (Random.Shared.NextSingle() - 0.5f) * 0.1f;
                float glitch = (i % 100 < 5) ? (Random.Shared.NextSingle() - 0.5f) * 0.5f : 0f;
                float mixed = output[i] * 0.8f + samples[i] * 0.2f + noise * 1000f + glitch * 5000f;
                samples[i] = (short)Math.Clamp(mixed, -32768, 32767);
            }
        }

        private void ApplyPhaseShifter(short[] samples, int count)
        {
            float phase = 0;
            float depth = 0.8f;
            float rate = 0.5f;

            for (int i = 0; i < count; i++)
            {
                phase += rate * 2f * MathF.PI / _sampleRate;
                if (phase > 2f * MathF.PI) phase -= 2f * MathF.PI;

                float shift = MathF.Sin(phase) * depth;
                int delaySamples = (int)(shift * 20f);
                int idx = Math.Max(0, i - Math.Abs(delaySamples));

                float pan = MathF.Sin(phase * 0.5f) * 0.5f + 0.5f;
                float mixed = (samples[idx] / 32768f) * (1f - pan) +
                              (samples[Math.Max(0, i - (int)(shift * 40f))] / 32768f) * pan;
                samples[i] = (short)Math.Clamp(mixed * 32768f, -32768, 32767);
            }
        }

        private void ApplyCombFilter(short[] samples, int count)
        {
            float feedback = 0.7f;
            int delaySamples = (int)(5f * _sampleRate / 1000f);
            float[] delayBuffer = new float[delaySamples];
            int writePos = 0;

            for (int i = 0; i < count; i++)
            {
                float input = samples[i] / 32768f;
                float delayed = delayBuffer[writePos];
                delayBuffer[writePos] = input + delayed * feedback;
                float output = input + delayed * 0.5f;
                samples[i] = (short)Math.Clamp(output * 32768f, -32768, 32767);
                writePos = (writePos + 1) % delaySamples;
            }
        }

        private void ApplySpectralFrost(short[] samples, int count)
        {
            int windowSize = 128;
            float freezeAmount = 0.7f;

            for (int i = 0; i < count; i++)
            {
                int windowPos = i % windowSize;
                int windowStart = i - windowPos;

                if (windowPos < windowSize * freezeAmount)
                {
                    int idx = windowStart;
                    if (idx < count)
                    {
                        samples[i] = samples[idx];
                    }
                }

                if (i % 10 == 0 && Random.Shared.NextSingle() > 0.8f)
                {
                    samples[i] = (short)(samples[i] * 0.5f);
                }
            }

            for (int i = 0; i < count; i++)
            {
                float mixed = samples[i] * 0.7f + samples[i] * 0.3f;
                samples[i] = (short)Math.Clamp(mixed, -32768, 32767);
            }
        }

     
        private void UpdateAudioMetrics(object? sender, EventArgs e)
        {
            if (!_isInitialized || _captureDevice == null || _captureApi == null || _alc == null) return;

            int samplesAvailable = 0;
            _alc.GetContextProperty(_captureDevice, (GetContextInteger)0x312, 1, &samplesAvailable);

            if (samplesAvailable <= 0) return;

            int samplesToRead = Math.Min(samplesAvailable, _audioBuffer.Length);
            fixed (short* ptr = _audioBuffer)
            {
                _captureApi.CaptureSamples(_captureDevice, ptr, samplesToRead);
            }

            if (_isLoopbackActive)
            {
                short[] processBuffer = new short[samplesToRead];
                Array.Copy(_audioBuffer, processBuffer, samplesToRead);
                ProcessAudioData(processBuffer, samplesToRead);
            }

            long sum = 0;
            int maxPeak = 0;
            for (int i = 0; i < samplesToRead; i++)
            {
                int sampleVal = _audioBuffer[i];
                sum += (long)sampleVal * sampleVal;
                int absVal = Math.Abs((int)sampleVal);
                if (absVal > maxPeak) maxPeak = absVal;
            }

            float rms = MathF.Sqrt(sum / (float)samplesToRead) / 32768f;
            double volumePercent = Math.Clamp(rms * 800.0, 0, 100);

            MicVolumeProgress.Value = volumePercent;
            OutputVolumeProgress.Value = _isLoopbackActive ? volumePercent : 0;

            float dbValue = 20 * MathF.Log10(maxPeak / 32768f + 0.0001f);
            DbText.Text = $"{dbValue:0.0} dBFS";

            UpdateBars(volumePercent);
        }

        private void UpdateBars(double volume)
        {
            Bar1.Value = Math.Clamp(volume * 1.5 + Random.Shared.Next(0, 4), 2, 100);
            Bar2.Value = Math.Clamp(volume * 1.8 + Random.Shared.Next(0, 5), 2, 100);
            Bar3.Value = Math.Clamp(volume * 1.3 + Random.Shared.Next(0, 4), 2, 100);
            Bar4.Value = Math.Clamp(volume * 1.0 + Random.Shared.Next(0, 3), 2, 100);
            Bar5.Value = Math.Clamp(volume * 1.1 + Random.Shared.Next(0, 3), 2, 100);
            Bar6.Value = Math.Clamp(volume * 0.7 + Random.Shared.Next(0, 2), 2, 100);
            Bar7.Value = Math.Clamp(volume * 0.5 + Random.Shared.Next(0, 2), 2, 100);
            Bar8.Value = Math.Clamp(volume * 0.3 + Random.Shared.Next(0, 2), 2, 100);
        }

     

        private void OnLoopbackToggleChanged(object? sender, RoutedEventArgs e)
        {
            _isLoopbackActive = LoopbackToggle.IsChecked ?? false;
            if (!_isLoopbackActive && _al != null && _sourceId != 0) _al.SourceStop(_sourceId);
        }

        private void OnVoicePresetChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (VoicePresetCombo.SelectedIndex >= 0) _selectedPreset = VoicePresetCombo.SelectedIndex;
        }

        private void OnNoiseToggleChanged(object? sender, RoutedEventArgs e)
        {
            _isNoiseSuppression = NoiseToggle.IsChecked ?? false;
        }

        private void OnCompressorToggleChanged(object? sender, RoutedEventArgs e)
        {
            _isCompressorActive = CompressorToggle.IsChecked ?? false;
        }

        private void OnMasterGainChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            _masterGain = (float)MasterSlider.Value;
            if (_al != null && _sourceId != 0) _al.SetSourceProperty(_sourceId, SourceFloat.Gain, _masterGain);
        }

        private void OnWetMixChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            _wetMix = (float)WetSlider.Value;
        }

        private void CleanupOpenAL()
        {
            try
            {
                _isInitialized = false;
                if (_al != null && _sourceId != 0)
                {
                    _al.SourceStop(_sourceId);
                    uint src = _sourceId;
                    _al.DeleteSources(1, &src);
                    fixed (uint* bPtr = _buffers) { _al.DeleteBuffers(NumBuffers, bPtr); }
                }
                if (_captureDevice != null && _captureApi != null)
                {
                    _captureApi.CaptureStop(_captureDevice);
                    _captureApi.CaptureCloseDevice(_captureDevice);
                }
                if (_context != null && _alc != null) _alc.DestroyContext(_context);
                if (_outputDevice != null && _alc != null) _alc.CloseDevice(_outputDevice);
                _captureApi?.Dispose();
                _alc?.Dispose();
                _al?.Dispose();
            }
            catch (Exception ex) { Debug.WriteLine($"❌ Cleanup Error: {ex.Message}"); }
        }
    }
}