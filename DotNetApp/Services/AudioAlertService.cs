using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace DotNetApp.Services
{
    public class AudioAlertService
    {
        private bool _isEnabled = true;
        private bool _isPlaying = false;
        private readonly object _lock = new();

        private static readonly byte[] _sirenWav;
        private static readonly byte[] _chimeWav;
        private static readonly byte[] _recoveryWav;

        static AudioAlertService()
        {
            // Synthesize authentic 16-bit PCM industrial audio in memory
            _sirenWav = SynthesizeWav(durationMs: 1200, freqMod: (t) => 1050.0 + 350.0 * Math.Sin(2.0 * Math.PI * 4.0 * t), volume: 22000);
            _chimeWav = SynthesizeWav(durationMs: 450, freqMod: (t) => t < 0.22 ? 880.0 : 1175.0, volume: 18000);
            _recoveryWav = SynthesizeWav(durationMs: 650, freqMod: (t) => t < 0.2 ? 523.25 : (t < 0.4 ? 659.25 : 783.99), volume: 16000);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        public void TriggerChime()
        {
            TriggerSiren();
        }

        public void TriggerSiren(bool force = false)
        {
            if ((!_isEnabled && !force) || _isPlaying) return;

            lock (_lock)
            {
                if (_isPlaying) return;
                _isPlaying = true;
            }

            Task.Run(() =>
            {
                try
                {
                    PlayWavWithFallback(_sirenWav, isEmergencySiren: true);
                }
                finally
                {
                    Task.Delay(1300).Wait();
                    lock (_lock) { _isPlaying = false; }
                }
            });
        }

        public void TriggerRecovery()
        {
            if (!_isEnabled || _isPlaying) return;

            lock (_lock)
            {
                if (_isPlaying) return;
                _isPlaying = true;
            }

            Task.Run(() =>
            {
                try
                {
                    PlayWavWithFallback(_recoveryWav, isEmergencySiren: false);
                }
                finally
                {
                    Task.Delay(800).Wait();
                    lock (_lock) { _isPlaying = false; }
                }
            });
        }

        public void PlayTestSound()
        {
            TriggerSiren(force: true);
        }

        private static void PlayWavWithFallback(byte[] wavBytes, bool isEmergencySiren)
        {
            bool played = false;
            try
            {
                using var ms = new MemoryStream(wavBytes);
                using var player = new SoundPlayer(ms);
                player.PlaySync();
                played = true;
            }
            catch
            {
                played = false;
            }

            if (!played)
            {
                try
                {
                    if (isEmergencySiren)
                    {
                        Console.Beep(1200, 250);
                        Console.Beep(850, 250);
                        Console.Beep(1200, 350);
                    }
                    else
                    {
                        Console.Beep(900, 200);
                        Console.Beep(1200, 250);
                    }
                }
                catch
                {
                    try
                    {
                        if (isEmergencySiren)
                            SystemSounds.Hand.Play();
                        else
                            SystemSounds.Asterisk.Play();
                    }
                    catch { }
                }
            }
        }

        private static byte[] SynthesizeWav(int durationMs, Func<double, double> freqMod, double volume)
        {
            int sampleRate = 22050;
            int totalSamples = sampleRate * durationMs / 1000;
            short[] samples = new short[totalSamples];

            double phase = 0.0;
            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double freq = freqMod(t);
                phase += 2.0 * Math.PI * freq / sampleRate;
                if (phase > 2.0 * Math.PI) phase -= 2.0 * Math.PI;

                double env = 1.0;
                int ramp = Math.Min(300, totalSamples / 4);
                if (i < ramp) env = (double)i / ramp;
                else if (i > totalSamples - ramp) env = (double)(totalSamples - i) / ramp;

                samples[i] = (short)(Math.Sin(phase) * volume * env);
            }

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            int dataSize = totalSamples * 2;

            bw.Write(new char[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataSize);
            bw.Write(new char[] { 'W', 'A', 'V', 'E' });
            bw.Write(new char[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1); // PCM format
            bw.Write((short)1); // Mono
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2); // Byte rate
            bw.Write((short)2); // Block align
            bw.Write((short)16); // Bits per sample
            bw.Write(new char[] { 'd', 'a', 't', 'a' });
            bw.Write(dataSize);

            for (int i = 0; i < totalSamples; i++)
            {
                bw.Write(samples[i]);
            }

            bw.Flush();
            return ms.ToArray();
        }
    }
}
